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

        public void EvaluateForPlayerAction(MatchActionSnapshot action) {
            if (board == null
                || registry == null
                || oneVanishSettings == null
                || action == null) {
                return;
            }

            if (board.IsVersusArena && board.VersusLayout != null) {
                foreach (var slot in action.GetParticipatingPlayers()) {
                    EvaluateForPlayerSlot(action.GetDiceFor(slot), slot);
                }

                return;
            }

            EvaluateGlobal(action.AllDice);
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

            if (board.IsVersusArena && board.VersusLayout != null) {
                EvaluateForPlayerSlot(snapshot.Participants, snapshot.Attacker);
                return;
            }

            EvaluateGlobal(snapshot.Participants);
        }

        void EvaluateGlobal(IReadOnlyCollection<DiceController> actionDice) {
            if (!DiceOneVanishTrigger.ShouldTrigger(registry.AllDice, actionDice)) {
                return;
            }

            VanishOnesMatching(
                dice => true,
                GetExcludedStandingDiceForAllPlayers());
        }

        void EvaluateForPlayerSlot(IReadOnlyList<DiceController> actionDice, PlayerSlot slot) {
            if (actionDice == null || actionDice.Count == 0) {
                return;
            }

            if (!DiceOneVanishTrigger.ShouldTrigger(registry.AllDice, actionDice)) {
                return;
            }

            var layout = board.VersusLayout;
            VanishOnesMatching(
                dice => layout.IsInsidePlayerRegion(slot, dice.CurrentState.GridPos),
                GetExcludedStandingDiceForPlayer(slot));
        }

        void VanishOnesMatching(System.Func<DiceController, bool> includeDice, HashSet<DiceController> excludedDice) {
            var targets = new List<DiceController>();

            foreach (var dice in registry.AllDice) {
                if (dice == null
                    || dice.IsSpawning
                    || dice.IsVanishing
                    || dice.IsDissolving
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

            foreach (var dice in targets) {
                dice.BeginOneVanish(oneVanishSettings, null);
            }
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
