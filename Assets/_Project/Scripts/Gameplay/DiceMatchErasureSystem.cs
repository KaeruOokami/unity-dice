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
        readonly Dictionary<DiceController, Action<DiceState>> diceStateHandlers = new();
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
            if (ownershipContext == null || fallenDice == null) {
                return;
            }

            try {
                if (!ownershipContext.ShouldEvaluateTierFall(fallenDice)
                    || !ownershipContext.TryResolveTierFallAttacker(fallenDice, out var attacker)) {
                    return;
                }

                var snapshot = new DeferredMatchSnapshot(fallenDice, attacker);
                EvaluateMatchesForDeferred(snapshot);
                oneVanishSystem?.EvaluateForDeferredAction(snapshot);
            } finally {
                ownershipContext.UnregisterReservation(fallenDice);
            }
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
            dice.ErasureStarted += OnDiceErasureStarted;
            dice.BecameErasureGhost += OnDiceBecameErasureGhost;
            dice.ConfigureTierFallMatchNotifier(this);

            Action<DiceState> stateHandler = _ => OnDiceStateCommitted(dice);
            diceStateHandlers[dice] = stateHandler;
            dice.StateChanged += stateHandler;

            if (dice.CurrentState.Tier == DiceStackTier.Top) {
                ownershipContext?.SyncReservationForTop(dice);
            }
        }

        void UnsubscribeDice(DiceController dice) {
            if (dice == null) {
                return;
            }

            dice.Erased -= OnDiceErased;
            dice.ErasureStarted -= OnDiceErasureStarted;
            dice.BecameErasureGhost -= OnDiceBecameErasureGhost;

            if (diceStateHandlers.TryGetValue(dice, out var stateHandler)) {
                dice.StateChanged -= stateHandler;
                diceStateHandlers.Remove(dice);
            }
        }

        void UnsubscribeAllDice() {
            foreach (var dice in subscribedDice) {
                UnsubscribeDice(dice);
            }

            subscribedDice.Clear();
            diceStateHandlers.Clear();
        }

        void OnDiceStateCommitted(DiceController dice) {
            if (dice == null || ownershipContext == null) {
                return;
            }

            if (dice.CurrentState.Tier == DiceStackTier.Top) {
                ownershipContext.SyncReservationForTop(dice);
            }
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

        void OnDiceErasureStarted(DiceController dice) {
            if (dice == null
                || ownershipContext == null
                || registry == null
                || !dice.IsSinkErasing) {
                return;
            }

            if (registry.TryGetTopAt(dice.CurrentState.GridPos, out var top)
                && top != null
                && top != dice) {
                ownershipContext.SyncReservationForTop(top);
            }
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

            var clusters = DiceMatchFinder.FindMatchingClusters(registry.AllDice, action.AllDice);
            foreach (var cluster in clusters) {
                var attacker = ResolveAttacker(action, cluster);
                ProcessCluster(cluster, attacker);
            }
        }

        void EvaluateMatchesForDeferred(DeferredMatchSnapshot snapshot) {
            if (snapshot == null
                || snapshot.Participants == null
                || snapshot.Participants.Count == 0
                || board == null
                || registry == null) {
                return;
            }

            var clusters = DiceMatchFinder.FindMatchingClusters(registry.AllDice, snapshot.Participants);
            foreach (var cluster in clusters) {
                ProcessCluster(cluster, snapshot.Attacker);
            }
        }

        void ProcessCluster(List<DiceController> cluster, PlayerSlot attacker) {
            var newMembers = new List<DiceController>();
            var erasingMembers = new List<DiceController>();

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

            if (newMembers.Count == 0) {
                return;
            }

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

        static PlayerSlot ResolveAttacker(MatchActionSnapshot action, List<DiceController> cluster) {
            foreach (var slot in action.GetParticipatingPlayers()) {
                var actionDice = action.GetDiceFor(slot);
                for (var i = 0; i < actionDice.Count; i++) {
                    var dice = actionDice[i];
                    if (dice == null) {
                        continue;
                    }

                    for (var j = 0; j < cluster.Count; j++) {
                        if (cluster[j] == dice) {
                            return slot;
                        }
                    }
                }
            }

            foreach (var slot in action.GetParticipatingPlayers()) {
                return slot;
            }

            return PlayerSlot.Player1;
        }
    }
}
