using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
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

        void OnActionCompleted(IReadOnlyCollection<DiceController> actionDice) {
            if (registry != null) {
                foreach (var dice in registry.AllDice) {
                    SubscribeDice(dice);
                }
            }

            EvaluateMatchesForAction(actionDice);
            oneVanishSystem?.EvaluateForPlayerAction(actionDice);
        }

        void OnDiceDissolved(DiceController dice) {
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

        void EvaluateMatchesForAction(IReadOnlyCollection<DiceController> actionDice) {
            if (actionDice == null
                || actionDice.Count == 0
                || board == null
                || registry == null) {
                return;
            }

            var clusters = DiceMatchFinder.FindMatchingClusters(registry.AllDice, actionDice);
            foreach (var cluster in clusters) {
                ProcessCluster(cluster);
            }
        }

        void ProcessCluster(List<DiceController> cluster) {
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

            foreach (var dice in dissolvingMembers) {
                dice.RetreatDissolve(chainRollbackAmount);
            }

            foreach (var dice in newMembers) {
                dice.BeginDissolve(null);
            }
        }
    }
}
