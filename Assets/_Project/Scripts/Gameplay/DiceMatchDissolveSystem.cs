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
    public class DiceMatchDissolveSystem : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] DiceRegistry registry;
        [SerializeField] PlayerMatchActionContext actionContext;
        [SerializeField] DiceOneVanishSystem oneVanishSystem;
        [SerializeField] float chainRollbackAmount = 0.15f;

        readonly HashSet<DiceController> subscribedDice = new();
        readonly List<CharacterController> characters = new();

        VersusBoardSettings versusSettings;
        SinkingGroupTracker sinkingGroups;
        MatchActionSnapshot currentAction;
        bool versusAttackEnabled;

        public event Action<ErasureResolvedEvent> ErasureResolved;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            IReadOnlyList<CharacterController> targetCharacters,
            PlayerMatchActionContext targetActionContext,
            DiceOneVanishSystem targetOneVanishSystem) {
            board = targetBoard;
            registry = targetRegistry;
            actionContext = targetActionContext;
            oneVanishSystem = targetOneVanishSystem;
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

        void OnDisable() {
            if (actionContext != null) {
                actionContext.ActionCompleted -= OnActionCompleted;
            }

            UnsubscribeAllDice();
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
            dice.Dissolved += OnDiceDissolved;
            dice.BecameDissolveGhost += OnDiceBecameDissolveGhost;
        }

        void UnsubscribeAllDice() {
            foreach (var dice in subscribedDice) {
                if (dice == null) {
                    continue;
                }

                dice.Dissolved -= OnDiceDissolved;
                dice.BecameDissolveGhost -= OnDiceBecameDissolveGhost;
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

        void OnDiceDissolved(DiceController dice) {
            sinkingGroups?.RemoveDice(dice);

            if (dice != null) {
                subscribedDice.Remove(dice);
                dice.Dissolved -= OnDiceDissolved;
                dice.BecameDissolveGhost -= OnDiceBecameDissolveGhost;
            }

            NotifyCharactersStandingDiceDissolved(dice);
        }

        void OnDiceBecameDissolveGhost(DiceController dice) {
            NotifyCharactersStandingDiceBecameGhost(dice);
        }

        void NotifyCharactersStandingDiceDissolved(DiceController dice) {
            for (var i = 0; i < characters.Count; i++) {
                characters[i]?.OnStandingDiceDissolved(dice);
            }
        }

        void NotifyCharactersStandingDiceBecameGhost(DiceController dice) {
            for (var i = 0; i < characters.Count; i++) {
                characters[i]?.OnStandingDiceBecameGhost(dice);
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
                ProcessCluster(cluster, action);
            }
        }

        void ProcessCluster(List<DiceController> cluster, MatchActionSnapshot action) {
            var newMembers = new List<DiceController>();
            var dissolvingMembers = new List<DiceController>();

            foreach (var dice in cluster) {
                if (dice == null) {
                    continue;
                }

                if (dice.IsDissolving) {
                    dissolvingMembers.Add(dice);
                } else if (!dice.IsSpawning) {
                    newMembers.Add(dice);
                }
            }

            if (newMembers.Count == 0) {
                return;
            }

            var face = newMembers[0].CurrentState.Orientation.Top;

            if (versusAttackEnabled && versusSettings != null && action != null) {
                ProcessVersusCluster(
                    action,
                    cluster,
                    newMembers,
                    dissolvingMembers,
                    face);
                return;
            }

            foreach (var dice in dissolvingMembers) {
                dice.RetreatDissolve(chainRollbackAmount);
            }

            foreach (var dice in newMembers) {
                dice.BeginDissolve(null);
            }
        }

        void ProcessVersusCluster(
            MatchActionSnapshot action,
            List<DiceController> cluster,
            List<DiceController> newMembers,
            List<DiceController> dissolvingMembers,
            int face) {
            var attacker = ResolveAttacker(action, cluster);
            var attackSettings = versusSettings.GetAttackSettings(attacker);
            if (attackSettings == null) {
                Debug.LogError($"DiceMatchDissolveSystem: Attack settings missing for {attacker}.");
                return;
            }

            var emissionColor = attackSettings.DissolveEmissionColor;
            var chainResult = sinkingGroups.RegisterCluster(
                dissolvingMembers,
                newMembers,
                face,
                attacker,
                out var clusterSize);

            foreach (var dice in dissolvingMembers) {
                dice.RetreatDissolve(chainRollbackAmount);
                dice.SetDissolveEmissionColor(emissionColor);
            }

            foreach (var dice in newMembers) {
                dice.BeginDissolve(emissionColor, null);
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
