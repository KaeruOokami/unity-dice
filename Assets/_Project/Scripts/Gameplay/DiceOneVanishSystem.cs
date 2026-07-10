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
        [SerializeField] CharacterController character;
        [SerializeField] DiceOneVanishSettings oneVanishSettings;

        readonly HashSet<DiceController> subscribedDice = new();

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            CharacterController targetCharacter,
            DiceOneVanishSettings settings) {
            board = targetBoard;
            registry = targetRegistry;
            character = targetCharacter;
            oneVanishSettings = settings;

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

            var excludedDice = character != null ? character.CurrentDice : null;
            var targets = new List<DiceController>();

            foreach (var dice in registry.AllDice) {
                if (dice == null
                    || dice.IsSpawning
                    || dice.IsVanishing
                    || dice.IsDissolving
                    || dice.CurrentState.Orientation.Top != 1) {
                    continue;
                }

                if (dice == excludedDice) {
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
