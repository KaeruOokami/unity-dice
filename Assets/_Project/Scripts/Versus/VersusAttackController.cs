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
    sealed class PendingAttackSend
    {
        public PlayerSlot Target;
        public AttackVolley Volley;
        public float RemainingTravel;
    }

    public sealed class VersusAttackController : MonoBehaviour
    {
        IBoardSettings versusSettings;
        Board board;
        DiceRegistry registry;
        DiceSpawnSystem spawnSystem;
        DiceMatchErasureSystem erasureSystem;
        AttackQueueSettings attackQueueSettings;
        DiceVfxSettings vfxSettings;
        float queueToBoardDelay;
        AttackQueueView queueView;
        System.Random random;
        Coroutine iconWarmupRoutine;

        readonly Dictionary<PlayerSlot, AttackQueue> incomingQueues = new();
        readonly Dictionary<PlayerSlot, float> naturalSendCooldowns = new();
        readonly List<PendingAttackSend> pendingAttackSends = new();
        bool gameplayEnabled = true;
        bool generateAttacks = true;
        bool applyQueuedSpawns = true;
        bool naturalSendActive;

        public bool AreIconsReady => queueView == null || queueView.AreIconsReady;

        public void Configure(
            IBoardSettings settings,
            Board targetBoard,
            DiceRegistry targetRegistry,
            DiceSpawnSystem targetSpawnSystem,
            DiceMatchErasureSystem targetErasureSystem,
            System.Random attackRandom,
            Transform viewParent,
            AttackQueueSettings queueSettings,
            float queueDelay,
            DiceVfxSettings diceVfxSettings = null) {
            versusSettings = settings;
            board = targetBoard;
            registry = targetRegistry;
            spawnSystem = targetSpawnSystem;
            erasureSystem = targetErasureSystem;
            attackQueueSettings = queueSettings;
            vfxSettings = diceVfxSettings;
            queueToBoardDelay = Mathf.Max(0f, queueDelay);
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
            FlushPendingAttackSends();

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

            TickPendingAttackSends(deltaTime);

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
                FlushPendingAttackSends();
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
                attackQueueSettings);
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
                    EnsureQueues();
                    incomingQueues[target].Enqueue(volley, queueToBoardDelay);
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
                    out var volley,
                    out _)) {
                return;
            }

            var maxSendCount = 0;
            if (attackSettings.TryGetSendableKindsForFace(e.Face, out var sendableKinds)) {
                maxSendCount = AttackVolumeResolver.ResolveAbsoluteMaxVolleyCount(sendableKinds);
            }

            if (maxSendCount <= 0) {
                Debug.LogError(
                    "VersusAttackController: Attack send max count is 0; VFX tier falls back to sent count only.");
                maxSendCount = Mathf.Max(1, volley.Count);
            }

            EnsureQueues();
            BeginAttackSend(
                e.Target,
                volley,
                minSendCount: 1,
                maxSendCount,
                e.OriginWorldPosition,
                attackSettings.ErasureEmissionColor);
        }

        void BeginAttackSend(
            PlayerSlot target,
            AttackVolley volley,
            int minSendCount,
            int maxSendCount,
            Vector3 startWorldPosition,
            Color emissionColor) {
            if (volley == null || volley.Count == 0) {
                return;
            }

            if (!TryPlayAttackSendVfx(
                    target,
                    volley.Count,
                    minSendCount,
                    maxSendCount,
                    startWorldPosition,
                    emissionColor,
                    out var travelDuration)) {
                incomingQueues[target].Enqueue(volley, queueToBoardDelay);
                return;
            }

            pendingAttackSends.Add(new PendingAttackSend {
                Target = target,
                Volley = volley,
                RemainingTravel = travelDuration
            });
        }

        bool TryPlayAttackSendVfx(
            PlayerSlot target,
            int sentCount,
            int minSendCount,
            int maxSendCount,
            Vector3 startWorldPosition,
            Color emissionColor,
            out float travelDuration) {
            travelDuration = 0f;

            if (vfxSettings == null) {
                Debug.LogError(
                    "VersusAttackController: Attack send VFX skipped because DiceVfxSettings is not assigned.");
                return false;
            }

            if (queueView == null) {
                Debug.LogError(
                    "VersusAttackController: Attack send VFX skipped because AttackQueueView is missing.");
                return false;
            }

            if (!queueView.AreIconsReady) {
                Debug.LogError(
                    "VersusAttackController: Attack send VFX skipped because queue icons are not ready.");
                return false;
            }

            if (!queueView.TryResolveAttackSendWorldPoints(
                    target,
                    startWorldPosition,
                    Camera.main,
                    out var startWorldOnCameraPlane,
                    out var endWorldOnCameraPlane)) {
                return false;
            }

            if (!AttackSendVfxPlayer.TryPlay(
                    vfxSettings,
                    sentCount,
                    minSendCount,
                    maxSendCount,
                    startWorldOnCameraPlane,
                    endWorldOnCameraPlane,
                    emissionColor)) {
                return false;
            }

            travelDuration = vfxSettings.AttackSendLifetime;
            return true;
        }

        void TickPendingAttackSends(float deltaTime) {
            if (pendingAttackSends.Count == 0) {
                return;
            }

            for (var i = 0; i < pendingAttackSends.Count; i++) {
                pendingAttackSends[i].RemainingTravel -= deltaTime;
            }

            for (var i = 0; i < pendingAttackSends.Count;) {
                var pending = pendingAttackSends[i];
                if (pending.RemainingTravel > 0f) {
                    i++;
                    continue;
                }

                EnsureQueues();
                incomingQueues[pending.Target].Enqueue(pending.Volley, queueToBoardDelay);
                pendingAttackSends.RemoveAt(i);
            }
        }

        void FlushPendingAttackSends() {
            if (pendingAttackSends.Count == 0) {
                return;
            }

            EnsureQueues();
            for (var i = 0; i < pendingAttackSends.Count; i++) {
                var pending = pendingAttackSends[i];
                incomingQueues[pending.Target].Enqueue(pending.Volley, queueToBoardDelay);
            }

            pendingAttackSends.Clear();
        }

        int ResolveJumboSendableRemaining(PlayerSlot targetBoard) {
            var jumboSettings = versusSettings != null ? versusSettings.JumboDiceSettings : null;
            if (jumboSettings == null || !jumboSettings.Enabled) {
                return 0;
            }

            EnsureQueues();
            var pending = CollectPendingVolleysForOccupancy(targetBoard);
            var occupied = JumboBoardOccupancy.CountOccupied(
                registry != null ? registry.AllDice : Array.Empty<DiceController>(),
                board != null ? board.VersusLayout : null,
                targetBoard,
                pending);
            return jumboSettings.GetRemainingSendableSlots(occupied);
        }

        IReadOnlyList<AttackVolley> CollectPendingVolleysForOccupancy(PlayerSlot targetBoard) {
            var combined = new List<AttackVolley>();
            if (incomingQueues.TryGetValue(targetBoard, out var queue)) {
                var queued = queue.GetPendingVolleys();
                for (var i = 0; i < queued.Count; i++) {
                    combined.Add(queued[i]);
                }
            }

            for (var i = 0; i < pendingAttackSends.Count; i++) {
                var pending = pendingAttackSends[i];
                if (pending.Target == targetBoard && pending.Volley != null) {
                    combined.Add(pending.Volley);
                }
            }

            return combined;
        }

        void TickQueue(PlayerSlot defenderSlot, float deltaTime) {
            if (!incomingQueues.TryGetValue(defenderSlot, out var queue)) {
                return;
            }

            while (queue.Count > 0) {
                if (!queue.IsHeadReady(deltaTime)) {
                    ApplyHeadDropAnticipation(defenderSlot, queue);
                    break;
                }

                if (queueView != null) {
                    queueView.ClearHeadDropAnticipation(defenderSlot);
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

            if (queue.Count == 0 && queueView != null) {
                queueView.ClearHeadDropAnticipation(defenderSlot);
            }
        }

        void ApplyHeadDropAnticipation(PlayerSlot defenderSlot, AttackQueue queue) {
            if (queueView == null || queue == null) {
                return;
            }

            if (!queue.TryGetHeadRemainingDelay(out var remainingDelay)) {
                queueView.ClearHeadDropAnticipation(defenderSlot);
                return;
            }

            queueView.ApplyHeadDropAnticipation(defenderSlot, remainingDelay);
        }

        AttackVolley SpawnVolley(PlayerSlot defenderSlot, AttackVolley volley) {
            var spawnSettings = versusSettings.GetSpawnSettings(defenderSlot);
            if (spawnSettings == null || volley == null) {
                return volley;
            }

            var remaining = new List<AttackDieSpec>();
            for (var i = 0; i < volley.Count; i++) {
                var spec = volley.Dice[i];
                var spawned = spawnSystem.SpawnAttackDice(
                    defenderSlot,
                    spec.Kind,
                    spec.Pip,
                    spawnSettings,
                    spec.IsOnFire);
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
                ReapplyHeadDropAnticipation(PlayerSlot.Player1);
                ReapplyHeadDropAnticipation(PlayerSlot.Player2);
            }

            QueuePresentationChanged?.Invoke();
        }

        void ReapplyHeadDropAnticipation(PlayerSlot defenderSlot) {
            if (!incomingQueues.TryGetValue(defenderSlot, out var queue)) {
                return;
            }

            ApplyHeadDropAnticipation(defenderSlot, queue);
        }

        public event Action QueuePresentationChanged;

        public IReadOnlyList<AttackVolley> GetPendingVolleys(PlayerSlot slot) {
            return incomingQueues.TryGetValue(slot, out var queue)
                ? queue.GetPendingVolleys()
                : Array.Empty<AttackVolley>();
        }
    }
}
