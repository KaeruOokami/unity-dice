using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.View;
using DiceGame.Versus.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DiceGame.Versus
{
    public sealed class VersusAttackController : MonoBehaviour
    {
        VersusBoardSettings versusSettings;
        DiceSpawnSystem spawnSystem;
        DiceMatchErasureSystem erasureSystem;
        AttackQueueView queueView;
        System.Random random;

        readonly Dictionary<PlayerSlot, AttackQueue> incomingQueues = new();
        readonly Dictionary<PlayerSlot, Coroutine> naturalSendCoroutines = new();
        bool gameplayEnabled = true;

        public void Configure(
            VersusBoardSettings settings,
            Board board,
            DiceSpawnSystem targetSpawnSystem,
            DiceMatchErasureSystem targetErasureSystem,
            System.Random attackRandom,
            Transform viewParent) {
            versusSettings = settings;
            spawnSystem = targetSpawnSystem;
            erasureSystem = targetErasureSystem;
            random = attackRandom ?? new System.Random();
            gameplayEnabled = true;

            if (erasureSystem != null) {
                erasureSystem.ErasureResolved -= OnErasureResolved;
                erasureSystem.ErasureResolved += OnErasureResolved;
            }

            EnsureQueues();
            EnsureQueueView(viewParent);
            StartNaturalSendLoops();
        }

        void OnDisable() {
            StopNaturalSendLoops();

            if (erasureSystem != null) {
                erasureSystem.ErasureResolved -= OnErasureResolved;
            }
        }

        void Update() {
            if (!gameplayEnabled || versusSettings == null || spawnSystem == null) {
                return;
            }

            TickQueue(PlayerSlot.Player1);
            TickQueue(PlayerSlot.Player2);
        }

        public void SetGameplayEnabled(bool enabled) {
            if (gameplayEnabled == enabled) {
                return;
            }

            gameplayEnabled = enabled;
            if (gameplayEnabled) {
                StartNaturalSendLoops();
            } else {
                StopNaturalSendLoops();
            }
        }

        void EnsureQueues() {
            if (!incomingQueues.ContainsKey(PlayerSlot.Player1)) {
                var queue = new AttackQueue();
                queue.Changed += RefreshQueueView;
                incomingQueues[PlayerSlot.Player1] = queue;
            }

            if (!incomingQueues.ContainsKey(PlayerSlot.Player2)) {
                var queue = new AttackQueue();
                queue.Changed += RefreshQueueView;
                incomingQueues[PlayerSlot.Player2] = queue;
            }
        }

        void EnsureQueueView(Transform viewParent) {
            if (queueView == null) {
                var viewObject = new GameObject("AttackQueueView");
                viewObject.transform.SetParent(viewParent != null ? viewParent : transform, false);
                queueView = viewObject.AddComponent<AttackQueueView>();
            }

            if (versusSettings == null) {
                return;
            }

            queueView.Configure(
                versusSettings.GetDiceCatalog(PlayerSlot.Player1),
                versusSettings.GetDiceCatalog(PlayerSlot.Player2),
                versusSettings.AttackQueueUiSettings);
            RefreshQueueView();
        }

        void StartNaturalSendLoops() {
            StopNaturalSendLoops();
            if (!gameplayEnabled) {
                return;
            }

            TryStartNaturalSendLoop(PlayerSlot.Player1);
            TryStartNaturalSendLoop(PlayerSlot.Player2);
        }

        void StopNaturalSendLoops() {
            foreach (var pair in naturalSendCoroutines) {
                if (pair.Value != null) {
                    StopCoroutine(pair.Value);
                }
            }

            naturalSendCoroutines.Clear();
        }

        void TryStartNaturalSendLoop(PlayerSlot sender) {
            if (versusSettings == null) {
                return;
            }

            var naturalSendSettings = versusSettings.GetNaturalSendSettings(sender);
            var spawnSettings = versusSettings.GetSpawnSettings(sender);
            if (naturalSendSettings == null
                || !naturalSendSettings.Enabled
                || spawnSettings == null) {
                return;
            }

            naturalSendCoroutines[sender] = StartCoroutine(
                NaturalSendLoop(sender, spawnSettings, naturalSendSettings));
        }

        IEnumerator NaturalSendLoop(
            PlayerSlot sender,
            DiceSpawnSettings spawnSettings,
            PlayerNaturalSendSettings naturalSendSettings) {
            while (enabled && gameplayEnabled) {
                var delay = spawnSettings.SpawnInterval
                    + Random.Range(-spawnSettings.SpawnIntervalJitter, spawnSettings.SpawnIntervalJitter);
                yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

                if (!NaturalSendVolleyBuilder.TryBuild(naturalSendSettings, random, out var volley)) {
                    continue;
                }

                var target = SinkingChainResolver.GetOpponent(sender);
                var attackSettings = versusSettings.GetAttackSettings(sender);
                var queueDelay = attackSettings != null
                    ? attackSettings.QueueToBoardDelay
                    : 0f;
                incomingQueues[target].Enqueue(volley, queueDelay);
            }
        }

        void OnErasureResolved(ErasureResolvedEvent e) {
            if (!gameplayEnabled || versusSettings == null) {
                return;
            }

            var attackSettings = versusSettings.GetAttackSettings(e.Attacker);
            if (attackSettings == null) {
                return;
            }

            if (!AttackVolleyBuilder.TryBuild(
                    attackSettings,
                    e.Face,
                    e.ChainCount,
                    e.ClusterSize,
                    e.IsSnatch,
                    random,
                    out var volley)) {
                return;
            }

            incomingQueues[e.Target].Enqueue(volley, attackSettings.QueueToBoardDelay);
        }

        void TickQueue(PlayerSlot defenderSlot) {
            if (!incomingQueues.TryGetValue(defenderSlot, out var queue)) {
                return;
            }

            while (queue.Count > 0) {
                if (!queue.IsHeadReady(Time.deltaTime)) {
                    break;
                }

                var volley = queue.PeekHead();
                var remaining = SpawnVolley(defenderSlot, volley);
                if (remaining == null || remaining.Count == 0) {
                    queue.DequeueHead();
                    continue;
                }

                queue.ReplaceHead(remaining);
                break;
            }
        }

        AttackVolley SpawnVolley(PlayerSlot defenderSlot, AttackVolley volley) {
            var spawnSettings = versusSettings.GetSpawnSettings(defenderSlot);
            if (spawnSettings == null || volley == null) {
                return volley;
            }

            var remaining = new List<AttackDieSpec>();
            for (var i = 0; i < volley.Count; i++) {
                var spec = volley.Dice[i];
                if (spawnSystem.SpawnAttackDice(defenderSlot, spec.Kind, spec.Pip, spawnSettings) == null) {
                    remaining.Add(spec);
                }
            }

            return new AttackVolley(remaining);
        }

        void RefreshQueueView() {
            if (queueView == null) {
                return;
            }

            queueView.RenderAll(
                incomingQueues[PlayerSlot.Player1].GetPendingVolleys(),
                incomingQueues[PlayerSlot.Player2].GetPendingVolleys());
        }
    }
}
