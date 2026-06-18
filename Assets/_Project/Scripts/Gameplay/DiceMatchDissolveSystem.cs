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

        readonly HashSet<DiceController> subscribedDice = new();

        public void Configure(Board targetBoard, DiceRegistry targetRegistry, CharacterController targetCharacter) {
            board = targetBoard;
            registry = targetRegistry;
            character = targetCharacter;
            SubscribeAllDice();
            EvaluateMatches();
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
            dice.StateChanged += OnDiceStateChanged;
            dice.Dissolved += OnDiceDissolved;
        }

        void UnsubscribeAllDice() {
            foreach (var dice in subscribedDice) {
                if (dice == null) {
                    continue;
                }

                dice.StateChanged -= OnDiceStateChanged;
                dice.Dissolved -= OnDiceDissolved;
            }

            subscribedDice.Clear();
        }

        void OnDiceStateChanged(DiceState state) {
            if (registry != null) {
                foreach (var dice in registry.AllDice) {
                    SubscribeDice(dice);
                }
            }

            EvaluateMatches();
        }

        void OnDiceDissolved(DiceController dice) {
            if (dice != null) {
                subscribedDice.Remove(dice);
            }

            character?.OnStandingDiceDissolved(dice);
            EvaluateMatches();
        }

        void EvaluateMatches() {
            if (board == null || registry == null || registry.AnyRolling()) {
                return;
            }

            var clusters = DiceMatchFinder.FindMatchingClusters(registry.AllDice);
            if (clusters.Count == 0) {
                return;
            }

            var dissolving = new HashSet<DiceController>();
            foreach (var cluster in clusters) {
                foreach (var dice in cluster) {
                    if (dice == null || dice.IsDissolving || dissolving.Contains(dice)) {
                        continue;
                    }

                    dissolving.Add(dice);
                    dice.BeginDissolve(null);
                }
            }
        }
    }
}
