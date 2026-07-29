using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.View;
using DiceGame.Versus.Core;
using UnityEngine;

namespace DiceGame.Versus
{
    public sealed class VersusAttackController : MonoBehaviour
    {
        IVersusBoardSettings versusSettings;
        Board board;
        DiceRegistry registry;
        DiceSpawnSystem spawnSystem;
        DiceMatchErasureSystem erasureSystem;
        AttackQueueView queueView;
        System.Random random;
        Coroutine iconWarmupRoutine;

        readonly Dictionary<PlayerSlot, AttackQueue> incomingQueues = new();
        readonly Dictionary<PlayerSlot, float> naturalSendCooldowns = new();
        bool gameplayEnabled = true;
        bool generateAttacks = true;
        bool applyQueuedSpawns = true;
        bool naturalSendActive;

        public bool AreIconsReady => queueView == null || queueView.AreIconsReady;

        public void Configure(
            IVersusBoardSettings settings,
            Board targetBoard,
            DiceRegistry targetRegistry,
            DiceSpawnSystem targetSpawnSystem,
            DiceMatchErasureSystem targetErasureSystem,
            System.Random attackRandom,
            Transform viewParent) {
            versusSettings = settings;
            board = targetBoard;
            registry = targetRegistry;
            spawnSystem = targetSpawnSystem;
            erasureSystem = targetErasureSystem;
            random = attackRandom ?? new System.Random();
            gameplayEnabled = true;
            generateAttacks = true;
            applyQueuedSpawns = true;

            if (erasureSystem != null) {
                erasureSystem.ErasureResolved -= OnErasureResolved;
                erasureSystem.ErasureResolved += OnErasureResolved;
            }

            EnsureQueues();
            EnsureQueueView(viewParent);
            if (iconWarmupRoutine != null) {
                StopCoroutine(iconWarmupRoutine);
            }

            iconWarmupRoutine = StartCoroutine(EnableGameplayAfterIconWarmup());
        }

        /// <summary>
        /// Online client full-sim experiment: do not generate volleys or spawn from queue.
        /// Host sends spawn commands; queue UI can still be updated via <see cref="ApplyNetworkQueuePresentation"/>.
        /// </summary>
        public void SetNetworkFollowerMode(bool follower) {
            generateAttacks = !follower;
            applyQueuedSpawns = !follower;
            if (follower) {
                StopNaturalSendLoops();
            } else if (gameplayEnabled && AreIconsReady) {
                StartNaturalSendLoops();
            }
        }

        public void ApplyNetworkQueuePresentation(
            IReadOnlyList<AttackVolley> player1Volleys,
            IReadOnlyList<AttackVolley> player2Volleys) {
            EnsureQueues();
            if (queueView != null) {
                queueView.RenderAll(player1Volleys, player2Volleys);
            }
        }

        void OnDisable() {
            if (iconWarmupRoutine != null) {
                StopCoroutine(iconWarmupRoutine);
                iconWarmupRoutine = null;
            }

            StopNaturalSendLoops();

            if (erasureSystem != null) {
                erasureSystem.ErasureResolved -= OnErasureResolved;
            }
        }

        void Update() {
            if (GameplaySimClock.IsActive) {
                return;
            }

            SimulateLockstepTick(Time.deltaTime);
        }

        /// <summary>
        /// Advance natural-send timers and attack queues (lockstep or offline).
        /// </summary>
        public void SimulateLockstepTick(float deltaTime) {
            if (!gameplayEnabled || versusSettings == null || spawnSystem == null || deltaTime <= 0f) {
                return;
            }

            if (!AreIconsReady) {
                return;
            }

            if (generateAttacks && naturalSendActive) {
                TickNaturalSend(PlayerSlot.Player1, deltaTime);
                TickNaturalSend(PlayerSlot.Player2, deltaTime);
            }

            if (!applyQueuedSpawns) {
                return;
            }

            TickQueue(PlayerSlot.Player1, deltaTime);
            TickQueue(PlayerSlot.Player2, deltaTime);
        }

        public void SetGameplayEnabled(bool enabled) {
            if (gameplayEnabled == enabled) {
                return;
            }

            gameplayEnabled = enabled;
            if (gameplayEnabled) {
                if (AreIconsReady) {
                    StartNaturalSendLoops();
                }
            } else {
                StopNaturalSendLoops();
            }
        }

        IEnumerator EnableGameplayAfterIconWarmup() {
            while (queueView != null && !queueView.AreIconsReady) {
                yield return null;
            }

            iconWarmupRoutine = null;
            if (gameplayEnabled && generateAttacks) {
                StartNaturalSendLoops();
            }

            RefreshQueueView();
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
            if (!gameplayEnabled || !generateAttacks || !AreIconsReady) {
                return;
            }

            naturalSendActive = true;
            TryArmNaturalSend(PlayerSlot.Player1);
            TryArmNaturalSend(PlayerSlot.Player2);
        }

        void StopNaturalSendLoops() {
            naturalSendActive = false;
            naturalSendCooldowns.Clear();
        }

        void TryArmNaturalSend(PlayerSlot sender) {
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

            naturalSendCooldowns[sender] = SampleNaturalSendDelay(spawnSettings);
        }

        float SampleNaturalSendDelay(DiceSpawnSettings spawnSettings) {
            var jitter = spawnSettings.SpawnIntervalJitter;
            var delay = spawnSettings.SpawnInterval
                + (float)((random.NextDouble() * 2.0 - 1.0) * jitter);
            return Mathf.Max(0.01f, delay);
        }

        void TickNaturalSend(PlayerSlot sender, float deltaTime) {
            if (!naturalSendCooldowns.TryGetValue(sender, out var cooldown)) {
                return;
            }

            var spawnSettings = versusSettings.GetSpawnSettings(sender);
            var naturalSendSettings = versusSettings.GetNaturalSendSettings(sender);
            if (spawnSettings == null || naturalSendSettings == null || !naturalSendSettings.Enabled) {
                return;
            }

            cooldown -= deltaTime;
            while (cooldown <= 0f) {
                var target = SinkingChainResolver.GetOpponent(sender);
                var jumboRemaining = ResolveJumboSendableRemaining(target);
                if (NaturalSendVolleyBuilder.TryBuild(
                        naturalSendSettings,
                        random,
                        jumboRemaining,
                        out var volley)) {
                    var attackSettings = versusSettings.GetAttackSettings(sender);
                    var queueDelay = attackSettings != null
                        ? attackSettings.QueueToBoardDelay
                        : 0f;
                    EnsureQueues();
                    incomingQueues[target].Enqueue(volley, queueDelay);
                }

                cooldown += SampleNaturalSendDelay(spawnSettings);
            }

            naturalSendCooldowns[sender] = cooldown;
        }

        void OnErasureResolved(ErasureResolvedEvent e) {
            if (!gameplayEnabled || !generateAttacks || versusSettings == null || !AreIconsReady) {
                return;
            }

            var attackSettings = versusSettings.GetAttackSettings(e.Attacker);
            if (attackSettings == null) {
                return;
            }

            var jumboRemaining = ResolveJumboSendableRemaining(e.Target);
            if (!AttackVolleyBuilder.TryBuild(
                    attackSettings,
                    e.Face,
                    e.ChainCount,
                    e.ClusterSize,
                    e.IsSnatch,
                    random,
                    jumboRemaining,
                    out var volley)) {
                return;
            }

            EnsureQueues();
            incomingQueues[e.Target].Enqueue(volley, attackSettings.QueueToBoardDelay);
        }

        int ResolveJumboSendableRemaining(PlayerSlot targetBoard) {
            var jumboSettings = versusSettings != null ? versusSettings.JumboDiceSettings : null;
            if (jumboSettings == null || !jumboSettings.Enabled) {
                return 0;
            }

            EnsureQueues();
            var pending = incomingQueues.TryGetValue(targetBoard, out var queue)
                ? queue.GetPendingVolleys()
                : Array.Empty<AttackVolley>();
            var occupied = JumboBoardOccupancy.CountOccupied(
                registry != null ? registry.AllDice : Array.Empty<DiceController>(),
                board != null ? board.VersusLayout : null,
                targetBoard,
                pending);
            return jumboSettings.GetRemainingSendableSlots(occupied);
        }

        void TickQueue(PlayerSlot defenderSlot, float deltaTime) {
            if (!incomingQueues.TryGetValue(defenderSlot, out var queue)) {
                return;
            }

            while (queue.Count > 0) {
                if (!queue.IsHeadReady(deltaTime)) {
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
                var spawned = spawnSystem.SpawnAttackDice(defenderSlot, spec.Kind, spec.Pip, spawnSettings);
                if (spawned == null) {
                    remaining.Add(spec);
                }
            }

            return new AttackVolley(remaining);
        }

        void RefreshQueueView() {
            if (queueView != null) {
                queueView.RenderAll(
                    incomingQueues[PlayerSlot.Player1].GetPendingVolleys(),
                    incomingQueues[PlayerSlot.Player2].GetPendingVolleys());
            }

            QueuePresentationChanged?.Invoke();
        }

        public event Action QueuePresentationChanged;

        public IReadOnlyList<AttackVolley> GetPendingVolleys(PlayerSlot slot) {
            return incomingQueues.TryGetValue(slot, out var queue)
                ? queue.GetPendingVolleys()
                : Array.Empty<AttackVolley>();
        }
    }
}
