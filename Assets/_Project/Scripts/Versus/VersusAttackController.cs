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
        DiceMatchDissolveSystem dissolveSystem;
        AttackQueueView queueView;
        System.Random random;

        readonly Dictionary<PlayerSlot, AttackQueue> incomingQueues = new();
        readonly Dictionary<PlayerSlot, Coroutine> naturalSendCoroutines = new();

        public void Configure(
            VersusBoardSettings settings,
            Board board,
            DiceSpawnSystem targetSpawnSystem,
            DiceMatchDissolveSystem targetDissolveSystem,
            System.Random attackRandom,
            Transform viewParent) {
            versusSettings = settings;
            spawnSystem = targetSpawnSystem;
            dissolveSystem = targetDissolveSystem;
            random = attackRandom ?? new System.Random();

            if (dissolveSystem != null) {
                dissolveSystem.ErasureResolved -= OnErasureResolved;
                dissolveSystem.ErasureResolved += OnErasureResolved;
            }

            EnsureQueues();
            EnsureQueueView(board, viewParent);
            StartNaturalSendLoops();
        }

        void OnDisable() {
            StopNaturalSendLoops();

            if (dissolveSystem != null) {
                dissolveSystem.ErasureResolved -= OnErasureResolved;
            }
        }

        void Update() {
            if (versusSettings == null || spawnSystem == null) {
                return;
            }

            TickQueue(PlayerSlot.Player1);
            TickQueue(PlayerSlot.Player2);
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

        void EnsureQueueView(Board board, Transform viewParent) {
            if (queueView == null) {
                var viewObject = new GameObject("AttackQueueView");
                viewObject.transform.SetParent(viewParent != null ? viewParent : transform, false);
                queueView = viewObject.AddComponent<AttackQueueView>();
            }

            if (versusSettings == null) {
                return;
            }

            queueView.Configure(
                board,
                versusSettings.GetDiceCatalog(PlayerSlot.Player1),
                versusSettings.GetDiceCatalog(PlayerSlot.Player2),
                viewParent != null ? viewParent : transform);
            RefreshQueueView();
        }

        void StartNaturalSendLoops() {
            StopNaturalSendLoops();
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
            while (enabled) {
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
            if (versusSettings == null) {
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

            while (queue.TryReleaseDue(Time.deltaTime, out var released)) {
                SpawnVolley(defenderSlot, released);
            }
        }

        void SpawnVolley(PlayerSlot defenderSlot, AttackVolley volley) {
            var spawnSettings = versusSettings.GetSpawnSettings(defenderSlot);
            if (spawnSettings == null || volley == null) {
                return;
            }

            for (var i = 0; i < volley.Count; i++) {
                var spec = volley.Dice[i];
                spawnSystem.SpawnAttackDice(defenderSlot, spec.Kind, spec.Pip, spawnSettings);
            }
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
