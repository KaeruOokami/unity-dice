using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
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
        IBoardSettings versusSettings;

        readonly HashSet<DiceController> subscribedDice = new();
        readonly List<CharacterController> characters = new();

        public event Action<PlayerSlot> FaceOneVanished;

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

        public void ConfigureVersusAttack(IBoardSettings settings) {
            versusSettings = settings;
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

        public void EvaluateForPlayerAction(MatchActionSnapshot action) {
            if (board == null
                || registry == null
                || oneVanishSettings == null
                || action == null) {
                return;
            }

            foreach (var slot in action.GetParticipatingPlayers()) {
                EvaluateForInitiator(
                    action.GetDiceFor(slot),
                    slot,
                    restrictToPlayerRegion: board.IsVersusArena && board.VersusLayout != null);
            }
        }

        public void EvaluateForDeferredAction(DeferredMatchSnapshot snapshot) {
            if (board == null
                || registry == null
                || oneVanishSettings == null
                || snapshot == null
                || snapshot.Participants == null
                || snapshot.Participants.Count == 0) {
                return;
            }

            EvaluateForInitiator(
                snapshot.Participants,
                snapshot.Attacker,
                restrictToPlayerRegion: board.IsVersusArena && board.VersusLayout != null);
        }

        void EvaluateForInitiator(
            IReadOnlyList<DiceController> actionDice,
            PlayerSlot initiator,
            bool restrictToPlayerRegion) {
            if (actionDice == null || actionDice.Count == 0) {
                return;
            }

            if (!DiceOneVanishTrigger.ShouldTrigger(registry.AllDice, actionDice)) {
                return;
            }

            System.Func<DiceController, bool> includeDice;
            HashSet<DiceController> excludedDice;
            if (restrictToPlayerRegion) {
                var layout = board.VersusLayout;
                includeDice = dice => layout.IsInsidePlayerRegion(initiator, dice.CurrentState.GridPos);
                excludedDice = GetExcludedStandingDiceForPlayer(initiator);
            } else {
                includeDice = dice => true;
                excludedDice = GetExcludedStandingDiceForAllPlayers();
            }

            VanishOnesMatching(includeDice, excludedDice, initiator);
        }

        void VanishOnesMatching(
            System.Func<DiceController, bool> includeDice,
            HashSet<DiceController> excludedDice,
            PlayerSlot initiator) {
            var targets = new List<DiceController>();

            foreach (var dice in registry.AllDice) {
                if (dice == null
                    || dice.IsSpawning
                    || dice.IsVanishing
                    || dice.IsErasing
                    || dice.CurrentState.Orientation.Top != 1) {
                    continue;
                }

                if (!includeDice(dice)) {
                    continue;
                }

                if (excludedDice.Contains(dice)) {
                    continue;
                }

                targets.Add(dice);
            }

            if (!TryGetPlayerEmissionColor(initiator, out var emissionColor)) {
                return;
            }

            var notified = false;
            foreach (var dice in targets) {
                var slot = initiator;
                dice.BeginOneVanish(oneVanishSettings, emissionColor, () => {
                    if (notified) {
                        return;
                    }

                    notified = true;
                    FaceOneVanished?.Invoke(slot);
                });
            }
        }

        bool TryGetPlayerEmissionColor(PlayerSlot initiator, out Color emissionColor) {
            emissionColor = Color.white;
            if (versusSettings == null) {
                return true;
            }

            var attack = versusSettings.GetAttackSettings(initiator);
            if (attack == null) {
                Debug.LogError($"DiceOneVanishSystem: Attack settings are missing for {initiator}.");
                return false;
            }

            emissionColor = attack.ErasureEmissionColor;
            return true;
        }

        HashSet<DiceController> GetExcludedStandingDiceForAllPlayers() {
            var excludedDice = new HashSet<DiceController>();
            for (var i = 0; i < characters.Count; i++) {
                var currentDice = characters[i] != null ? characters[i].CurrentDice : null;
                if (currentDice != null) {
                    excludedDice.Add(currentDice);
                }
            }

            return excludedDice;
        }

        HashSet<DiceController> GetExcludedStandingDiceForPlayer(PlayerSlot slot) {
            var excludedDice = new HashSet<DiceController>();
            var character = FindCharacter(slot);
            var currentDice = character != null ? character.CurrentDice : null;
            if (currentDice != null) {
                excludedDice.Add(currentDice);
            }

            return excludedDice;
        }

        CharacterController FindCharacter(PlayerSlot slot) {
            for (var i = 0; i < characters.Count; i++) {
                if (characters[i] != null && characters[i].PlayerSlot == slot) {
                    return characters[i];
                }
            }

            return null;
        }
    }
}
