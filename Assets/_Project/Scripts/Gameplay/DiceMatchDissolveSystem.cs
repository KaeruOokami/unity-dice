using System;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DiceMatchDissolveSystem : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] DiceRegistry registry;
        [SerializeField] CharacterController character;
        [SerializeField] float chainRollbackAmount = 0.15f;

        readonly HashSet<DiceController> subscribedDice = new();
        readonly Dictionary<DiceController, Action<DiceState>> diceStateHandlers = new();

        public void Configure(Board targetBoard, DiceRegistry targetRegistry, CharacterController targetCharacter) {
            board = targetBoard;
            registry = targetRegistry;
            character = targetCharacter;
            SubscribeAllDice();
        }

        void OnDisable() {
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
            Action<DiceState> handler = _ => OnDiceStateChanged(dice);
            diceStateHandlers[dice] = handler;
            dice.StateChanged += handler;
            dice.Dissolved += OnDiceDissolved;
            dice.BecameDissolveGhost += OnDiceBecameDissolveGhost;
        }

        void UnsubscribeAllDice() {
            foreach (var dice in subscribedDice) {
                if (dice == null) {
                    continue;
                }

                if (diceStateHandlers.TryGetValue(dice, out var handler)) {
                    dice.StateChanged -= handler;
                }

                dice.Dissolved -= OnDiceDissolved;
                dice.BecameDissolveGhost -= OnDiceBecameDissolveGhost;
            }

            subscribedDice.Clear();
            diceStateHandlers.Clear();
        }

        void OnDiceStateChanged(DiceController triggerDice) {
            if (registry != null) {
                foreach (var dice in registry.AllDice) {
                    SubscribeDice(dice);
                }
            }

            EvaluateMatches(triggerDice);
        }

        void OnDiceDissolved(DiceController dice) {
            if (dice != null) {
                subscribedDice.Remove(dice);
                diceStateHandlers.Remove(dice);
                dice.BecameDissolveGhost -= OnDiceBecameDissolveGhost;
            }

            character?.OnStandingDiceDissolved(dice);
        }

        void OnDiceBecameDissolveGhost(DiceController dice) {
            character?.OnStandingDiceBecameGhost(dice);
        }

        void EvaluateMatches(DiceController triggerDice) {
            if (triggerDice == null || board == null || registry == null || registry.AnyRolling()) {
                return;
            }

            var clusters = DiceMatchFinder.FindMatchingClusters(registry.AllDice);
            foreach (var cluster in clusters) {
                if (!cluster.Contains(triggerDice)) {
                    continue;
                }

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
                } else {
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
