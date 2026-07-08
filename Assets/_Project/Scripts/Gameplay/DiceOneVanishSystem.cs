using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
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
        readonly Dictionary<DiceController, Action<DiceState>> diceStateHandlers = new();

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
            Action<DiceState> stateHandler = _ => OnDiceStateChanged();
            diceStateHandlers[dice] = stateHandler;
            dice.StateChanged += stateHandler;
            dice.DissolveStarted += OnDissolveStarted;
            dice.Dissolved += OnDiceDissolved;
        }

        void UnsubscribeAllDice() {
            foreach (var dice in subscribedDice) {
                if (dice == null) {
                    continue;
                }

                if (diceStateHandlers.TryGetValue(dice, out var stateHandler)) {
                    dice.StateChanged -= stateHandler;
                }

                dice.DissolveStarted -= OnDissolveStarted;
                dice.Dissolved -= OnDiceDissolved;
            }

            subscribedDice.Clear();
            diceStateHandlers.Clear();
        }

        void OnDiceStateChanged() {
            if (registry != null) {
                foreach (var dice in registry.AllDice) {
                    SubscribeDice(dice);
                }
            }

            EvaluateOneVanish();
        }

        void OnDissolveStarted(DiceController _) {
            EvaluateOneVanish();
        }

        void OnDiceDissolved(DiceController dice) {
            if (dice != null) {
                subscribedDice.Remove(dice);
                diceStateHandlers.Remove(dice);
            }

            character?.OnStandingDiceDissolved(dice);
        }

        void EvaluateOneVanish() {
            if (board == null
                || registry == null
                || oneVanishSettings == null
                || registry.AnyRolling()) {
                return;
            }

            if (!DiceOneVanishTrigger.ShouldTrigger(registry.AllDice)) {
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
