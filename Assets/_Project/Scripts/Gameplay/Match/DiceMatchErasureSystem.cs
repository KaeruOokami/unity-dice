using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Versus;
using DiceGame.Versus.Core;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DiceMatchErasureSystem : MonoBehaviour, ITierFallMatchNotifier
    {
        [SerializeField] Board board;
        [SerializeField] DiceRegistry registry;
        [SerializeField] PlayerMatchActionContext actionContext;
        [SerializeField] DiceOneVanishSystem oneVanishSystem;
        [SerializeField] float chainRollbackAmount = 0.15f;

        readonly HashSet<DiceController> subscribedDice = new();
        readonly List<CharacterController> characters = new();
        readonly List<DiceController> pendingTierFallMatches = new();

        IVersusBoardSettings versusSettings;
        DiceErasureSettings erasureSettings;
        SinkingGroupTracker sinkingGroups;
        DiceMatchOwnershipContext ownershipContext;
        bool versusAttackEnabled;

        public event Action<ErasureResolvedEvent> ErasureResolved;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            IReadOnlyList<CharacterController> targetCharacters,
            PlayerMatchActionContext targetActionContext,
            DiceOneVanishSystem targetOneVanishSystem,
            DiceMatchOwnershipContext targetOwnershipContext,
            DiceErasureSettings targetErasureSettings) {
            board = targetBoard;
            registry = targetRegistry;
            actionContext = targetActionContext;
            oneVanishSystem = targetOneVanishSystem;
            ownershipContext = targetOwnershipContext;
            erasureSettings = targetErasureSettings;
            characters.Clear();
            if (targetCharacters != null) {
                characters.AddRange(targetCharacters);
            }

            if (actionContext != null) {
                actionContext.ActionCompleted -= OnActionCompleted;
                actionContext.ActionCompleted += OnActionCompleted;
            }

            SubscribeAllDice();
        }

        public void ConfigureSinkingChain() {
            sinkingGroups = board != null ? new SinkingGroupTracker() : null;
        }

        public void ConfigureVersusAttack(IVersusBoardSettings settings) {
            versusSettings = settings;
            versusAttackEnabled = settings != null && board != null && board.IsVersusArena;
            if (versusAttackEnabled && sinkingGroups == null) {
                ConfigureSinkingChain();
            }
        }

        public void EnsureDiceSubscribed(DiceController dice) {
            SubscribeDice(dice);
        }

        void Update() {
            TryFlushPendingTierFallMatches();
        }

        void OnDisable() {
            if (actionContext != null) {
                actionContext.ActionCompleted -= OnActionCompleted;
            }

            UnsubscribeAllDice();
            pendingTierFallMatches.Clear();
        }

        public void NotifyTierFallCompleted(DiceController fallenDice) {
            if (fallenDice == null || ownershipContext == null) {
                return;
            }

            pendingTierFallMatches.Add(fallenDice);
            TryFlushPendingTierFallMatches();
        }

        void TryFlushPendingTierFallMatches() {
            if (pendingTierFallMatches.Count == 0 || registry == null) {
                return;
            }

            for (var i = pendingTierFallMatches.Count - 1; i >= 0; i--) {
                var fallenDice = pendingTierFallMatches[i];
                if (fallenDice == null) {
                    pendingTierFallMatches.RemoveAt(i);
                    continue;
                }

                if (MotionConflictEvaluator.BlocksTierFallEvaluation(fallenDice, registry, actionContext)) {
                    continue;
                }

                pendingTierFallMatches.RemoveAt(i);
                EvaluateTierFallMatch(fallenDice);
            }
        }

        void EvaluateTierFallMatch(DiceController fallenDice) {
            if (fallenDice == null || board == null || registry == null || ownershipContext == null) {
                return;
            }

            var participants = new List<DiceController> { fallenDice };
            var clusters = DiceMatchFinder.FindMatchingClusters(registry.AllDice, participants);
            PlayerSlot? deferredAttacker = null;

            foreach (var cluster in clusters) {
                if (!MatchAttackerResolver.TryResolveAttackerForTierFall(
                    cluster.Members,
                    ownershipContext,
                    fallenDice,
                    out var attacker)) {
                    continue;
                }

                deferredAttacker ??= attacker;
                ProcessCluster(cluster, attacker);
            }

            if (deferredAttacker == null
                && MatchAttackerResolver.TryResolveAttackerForTierFall(
                    cluster: null,
                    ownershipContext,
                    fallenDice,
                    out var supportAttacker)) {
                deferredAttacker = supportAttacker;
            }

            if (deferredAttacker.HasValue) {
                oneVanishSystem?.EvaluateForDeferredAction(
                    new DeferredMatchSnapshot(fallenDice, deferredAttacker.Value));
            }

            ownershipContext.ClearTierFallSupportOwner(fallenDice);
        }

        void SubscribeAllDice() {
            if (registry == null) {
                return;
            }

            foreach (var dice in registry.AllDice) {
                SubscribeDice(dice);
            }
        }

        void SubscribeDice(DiceController dice) {
            if (dice == null || subscribedDice.Contains(dice)) {
                return;
            }

            subscribedDice.Add(dice);
            dice.Erased += OnDiceErased;
            dice.BecameErasureGhost += OnDiceBecameErasureGhost;
            dice.ConfigureOwnershipContext(ownershipContext);
            dice.ConfigureTierFallMatchNotifier(this);
        }

        void UnsubscribeDice(DiceController dice) {
            if (dice == null) {
                return;
            }

            dice.Erased -= OnDiceErased;
            dice.BecameErasureGhost -= OnDiceBecameErasureGhost;
        }

        void UnsubscribeAllDice() {
            foreach (var dice in subscribedDice) {
                UnsubscribeDice(dice);
            }

            subscribedDice.Clear();
        }

        void OnActionCompleted(MatchActionSnapshot action) {
            if (registry != null) {
                foreach (var dice in registry.AllDice) {
                    SubscribeDice(dice);
                }
            }

            if (action == null) {
                return;
            }

            EvaluateMatchesForAction(action);
            oneVanishSystem?.EvaluateForPlayerAction(action);
        }

        void OnDiceErased(DiceController dice) {
            sinkingGroups?.RemoveDice(dice);
            ownershipContext?.OnDiceRemoved(dice);

            if (dice != null) {
                subscribedDice.Remove(dice);
                UnsubscribeDice(dice);
            }

            NotifyCharactersStandingDiceErased(dice);
        }

        void OnDiceBecameErasureGhost(DiceController dice) {
            NotifyCharactersStandingDiceBecameGhost(dice);
        }

        void NotifyCharactersStandingDiceErased(DiceController dice) {
            for (var i = 0; i < characters.Count; i++) {
                characters[i]?.OnStandingDiceErased(dice);
            }
        }

        void NotifyCharactersStandingDiceBecameGhost(DiceController dice) {
            for (var i = 0; i < characters.Count; i++) {
                characters[i]?.OnStandingDiceBecameErasureGhost(dice);
            }
        }

        void EvaluateMatchesForAction(MatchActionSnapshot action) {
            if (action.AllDice == null
                || action.AllDice.Count == 0
                || board == null
                || registry == null) {
                return;
            }

            for (var i = 0; i < action.AllDice.Count; i++) {
                var triggerDice = action.AllDice[i];
                if (triggerDice == null) {
                    continue;
                }

                EvaluateMatchClustersForTrigger(triggerDice, action);
            }
        }

        void EvaluateMatchClustersForTrigger(DiceController triggerDice, MatchActionSnapshot action) {
            if (triggerDice == null
                || board == null
                || registry == null
                || ownershipContext == null) {
                return;
            }

            var participants = new List<DiceController> { triggerDice };
            var clusters = DiceMatchFinder.FindMatchingClusters(registry.AllDice, participants);
            foreach (var cluster in clusters) {
                if (!MatchAttackerResolver.TryResolveAttacker(
                    cluster.Members,
                    action,
                    participants,
                    ownershipContext,
                    board,
                    triggerDice,
                    out var attacker)) {
                    continue;
                }

                ProcessCluster(cluster, attacker);
            }
        }

        void ProcessCluster(DiceMatchCluster cluster, PlayerSlot attacker) {
            PartitionClusterMembers(cluster.Members, out var newMembers, out var erasingMembers);

            if (newMembers.Count > 0) {
                ApplyClusterErasure(cluster, newMembers, erasingMembers, attacker);
                return;
            }

            if (erasingMembers.Count > 0) {
                ProcessSinkingFollowUp(cluster, attacker);
            }
        }

        static void PartitionClusterMembers(
            List<DiceController> cluster,
            out List<DiceController> newMembers,
            out List<DiceController> erasingMembers) {
            newMembers = new List<DiceController>();
            erasingMembers = new List<DiceController>();

            if (cluster == null) {
                return;
            }

            foreach (var dice in cluster) {
                if (dice == null) {
                    continue;
                }

                if (dice.IsErasing) {
                    erasingMembers.Add(dice);
                } else if (!dice.IsSpawning) {
                    newMembers.Add(dice);
                }
            }
        }

        void ApplyClusterErasure(
            DiceMatchCluster cluster,
            List<DiceController> newMembers,
            List<DiceController> erasingMembers,
            PlayerSlot attacker) {
            if (!TryGetPlayerEmissionColor(attacker, out var emissionColor)) {
                return;
            }

            var face = newMembers[0].CurrentState.Orientation.Top;
            var chainResult = RegisterSinkingCluster(
                erasingMembers,
                newMembers,
                face,
                attacker,
                cluster.Weight,
                out var clusterSize);

            foreach (var dice in erasingMembers) {
                dice.RetreatErasure(chainRollbackAmount);
                dice.SetErasureEmissionColor(emissionColor);
            }

            AssignErasingOwners(newMembers, attacker);
            if (erasingMembers.Count > 0) {
                AssignErasingOwners(erasingMembers, attacker);
            }

            foreach (var dice in newMembers) {
                dice.BeginErasureForCurrentTier(emissionColor, null);
            }

            TryEmitVersusAttack(attacker, face, chainResult, clusterSize);
        }

        void ProcessSinkingFollowUp(DiceMatchCluster cluster, PlayerSlot attacker) {
            if (sinkingGroups == null) {
                return;
            }

            var chainResult = sinkingGroups.RegisterFollowUpAttack(
                cluster.Members,
                attacker,
                out var face,
                out _,
                cluster.Weight);

            if (chainResult.IsSnatch
                && TryGetPlayerEmissionColor(attacker, out var emissionColor)) {
                PartitionClusterMembers(cluster.Members, out _, out var erasingMembers);
                AssignErasingOwners(erasingMembers, attacker);
                for (var i = 0; i < erasingMembers.Count; i++) {
                    erasingMembers[i].SetErasureEmissionColor(emissionColor);
                }
            }

            TryEmitVersusAttack(attacker, face, chainResult, cluster.Weight);
        }

        SinkingChainResult RegisterSinkingCluster(
            IReadOnlyList<DiceController> erasingMembers,
            IReadOnlyList<DiceController> newMembers,
            int face,
            PlayerSlot attacker,
            int matchWeight,
            out int clusterSize) {
            clusterSize = matchWeight;
            if (sinkingGroups == null) {
                return new SinkingChainResult(0, false);
            }

            return sinkingGroups.RegisterCluster(
                erasingMembers,
                newMembers,
                face,
                attacker,
                out clusterSize,
                matchWeight);
        }

        void TryEmitVersusAttack(
            PlayerSlot attacker,
            int face,
            SinkingChainResult chainResult,
            int clusterSize) {
            if (!versusAttackEnabled || versusSettings == null) {
                return;
            }

            var target = SinkingChainResolver.GetOpponent(attacker);
            ErasureResolved?.Invoke(new ErasureResolvedEvent(
                attacker,
                target,
                face,
                chainResult.ChainCount,
                clusterSize,
                chainResult.IsSnatch));
        }

        bool TryGetPlayerEmissionColor(PlayerSlot attacker, out Color emissionColor) {
            emissionColor = default;
            if (erasureSettings == null) {
                Debug.LogError("DiceMatchErasureSystem: Erasure settings are not configured.");
                return false;
            }

            emissionColor = erasureSettings.GetPlayerEmissionColor(attacker);
            return true;
        }

        void AssignErasingOwners(IReadOnlyList<DiceController> members, PlayerSlot attacker) {
            if (ownershipContext == null || members == null) {
                return;
            }

            for (var i = 0; i < members.Count; i++) {
                ownershipContext.SetOwner(members[i], attacker);
            }
        }

    }
}
