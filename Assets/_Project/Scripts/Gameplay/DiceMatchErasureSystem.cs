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

        VersusBoardSettings versusSettings;
        SinkingGroupTracker sinkingGroups;
        DiceMatchOwnershipContext ownershipContext;
        MatchActionSnapshot currentAction;
        bool versusAttackEnabled;

        public event Action<ErasureResolvedEvent> ErasureResolved;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            IReadOnlyList<CharacterController> targetCharacters,
            PlayerMatchActionContext targetActionContext,
            DiceOneVanishSystem targetOneVanishSystem,
            DiceMatchOwnershipContext targetOwnershipContext) {
            board = targetBoard;
            registry = targetRegistry;
            actionContext = targetActionContext;
            oneVanishSystem = targetOneVanishSystem;
            ownershipContext = targetOwnershipContext;
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

        public void ConfigureVersusAttack(VersusBoardSettings settings) {
            versusSettings = settings;
            versusAttackEnabled = settings != null && board != null && board.IsVersusArena;
            sinkingGroups = versusAttackEnabled ? new SinkingGroupTracker() : null;
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
            if (pendingTierFallMatches.Count == 0
                || registry == null
                || (registry.AnyRolling() || registry.AnyCarried())) {
                return;
            }

            var pending = new List<DiceController>(pendingTierFallMatches);
            pendingTierFallMatches.Clear();

            for (var i = 0; i < pending.Count; i++) {
                EvaluateTierFallMatch(pending[i]);
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
                if (!MatchAttackerResolver.TryResolveAttacker(
                    cluster,
                    currentAction,
                    participants,
                    ownershipContext,
                    board,
                    fallenDice,
                    out var attacker)) {
                    continue;
                }

                deferredAttacker ??= attacker;
                ProcessCluster(cluster, attacker);
            }

            if (!MatchAttackerResolver.TryResolveAttackerForDice(
                fallenDice,
                ownershipContext,
                board,
                out var vanishAttacker)) {
                return;
            }

            deferredAttacker ??= vanishAttacker;
            oneVanishSystem?.EvaluateForDeferredAction(
                new DeferredMatchSnapshot(fallenDice, deferredAttacker.Value));
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
            currentAction = action;

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
                    cluster,
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

        void ProcessCluster(List<DiceController> cluster, PlayerSlot attacker) {
            PartitionClusterMembers(cluster, out var newMembers, out var erasingMembers);

            if (newMembers.Count > 0) {
                ApplyClusterErasure(cluster, newMembers, erasingMembers, attacker);
                return;
            }

            if (erasingMembers.Count > 0) {
                EmitVersusFollowUpAttack(cluster, attacker);
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
            List<DiceController> cluster,
            List<DiceController> newMembers,
            List<DiceController> erasingMembers,
            PlayerSlot attacker) {
            var face = newMembers[0].CurrentState.Orientation.Top;

            if (versusAttackEnabled && versusSettings != null) {
                ProcessVersusCluster(
                    cluster,
                    newMembers,
                    erasingMembers,
                    face,
                    attacker);
                return;
            }

            foreach (var dice in erasingMembers) {
                dice.RetreatErasure(chainRollbackAmount);
            }

            AssignErasingOwners(newMembers, attacker);
            foreach (var dice in newMembers) {
                dice.BeginErasureForCurrentTier(null, null);
            }
        }

        void EmitVersusFollowUpAttack(List<DiceController> cluster, PlayerSlot attacker) {
            if (!versusAttackEnabled || versusSettings == null || sinkingGroups == null) {
                return;
            }

            var attackSettings = versusSettings.GetAttackSettings(attacker);
            if (attackSettings == null) {
                Debug.LogError($"DiceMatchErasureSystem: Attack settings missing for {attacker}.");
                return;
            }

            var chainResult = sinkingGroups.RegisterFollowUpAttack(
                cluster,
                attacker,
                out var face,
                out var clusterSize);

            var target = SinkingChainResolver.GetOpponent(attacker);
            ErasureResolved?.Invoke(new ErasureResolvedEvent(
                attacker,
                target,
                face,
                chainResult.ChainCount,
                clusterSize,
                chainResult.IsSnatch));
        }

        void ProcessVersusCluster(
            List<DiceController> cluster,
            List<DiceController> newMembers,
            List<DiceController> erasingMembers,
            int face,
            PlayerSlot attacker) {
            var attackSettings = versusSettings.GetAttackSettings(attacker);
            if (attackSettings == null) {
                Debug.LogError($"DiceMatchErasureSystem: Attack settings missing for {attacker}.");
                return;
            }

            var emissionColor = attackSettings.ErasureEmissionColor;
            var chainResult = sinkingGroups.RegisterCluster(
                erasingMembers,
                newMembers,
                face,
                attacker,
                out var clusterSize);

            foreach (var dice in erasingMembers) {
                dice.RetreatErasure(chainRollbackAmount);
                dice.SetErasureEmissionColor(emissionColor);
            }

            AssignErasingOwners(newMembers, attacker);
            foreach (var dice in newMembers) {
                dice.BeginErasureForCurrentTier(emissionColor, null);
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

        void AssignErasingOwners(IReadOnlyList<DiceController> newMembers, PlayerSlot attacker) {
            if (ownershipContext == null || newMembers == null) {
                return;
            }

            for (var i = 0; i < newMembers.Count; i++) {
                ownershipContext.SetOwner(newMembers[i], attacker);
            }
        }

    }
}
