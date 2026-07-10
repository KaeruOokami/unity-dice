using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DiceOneVanishSystem : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] DiceRegistry registry;
        [SerializeField] DiceOneVanishSettings oneVanishSettings;

        readonly HashSet<DiceController> subscribedDice = new();
        readonly List<CharacterController> characters = new();

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            IReadOnlyList<CharacterController> targetCharacters,
            DiceOneVanishSettings settings) {
            board = targetBoard;
            registry = targetRegistry;
            oneVanishSettings = settings;
            characters.Clear();
            if (targetCharacters != null) {
                characters.AddRange(targetCharacters);
            }

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
        }

        void UnsubscribeAllDice() {
            subscribedDice.Clear();
        }

        public void EvaluateForPlayerAction(IReadOnlyCollection<DiceController> actionDice) {
            if (board == null
                || registry == null
                || oneVanishSettings == null) {
                return;
            }

            if (!DiceOneVanishTrigger.ShouldTrigger(registry.AllDice, actionDice)) {
                return;
            }

            var excludedDice = new HashSet<DiceController>();
            for (var i = 0; i < characters.Count; i++) {
                var currentDice = characters[i] != null ? characters[i].CurrentDice : null;
                if (currentDice != null) {
                    excludedDice.Add(currentDice);
                }
            }

            var targets = new List<DiceController>();

            foreach (var dice in registry.AllDice) {
                if (dice == null
                    || dice.IsSpawning
                    || dice.IsVanishing
                    || dice.IsDissolving
                    || dice.CurrentState.Orientation.Top != 1) {
                    continue;
                }

                if (excludedDice.Contains(dice)) {
                    continue;
                }

                targets.Add(dice);
            }

            foreach (var dice in targets) {
                dice.BeginOneVanish(oneVanishSettings, null);
            }
        }
    }
}
