using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;

namespace DiceGame.Versus.Core
{
    public static class JumboBoardOccupancy
    {
        public static int CountOccupied(
            IReadOnlyList<DiceController> allDice,
            VersusArenaLayout layout,
            PlayerSlot boardOwner,
            IReadOnlyList<AttackVolley> pendingVolleys) {
            return CountOnBoard(allDice, layout, boardOwner) + CountQueued(pendingVolleys);
        }

        public static int CountOnBoard(
            IReadOnlyList<DiceController> allDice,
            VersusArenaLayout layout,
            PlayerSlot boardOwner) {
            if (allDice == null || allDice.Count == 0 || layout == null) {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                if (dice == null || dice.Kind != DiceKind.Jumbo) {
                    continue;
                }

                if (!layout.IsInsidePlayerRegion(boardOwner, dice.CurrentState.GridPos)) {
                    continue;
                }

                count++;
            }

            return count;
        }

        public static int CountQueued(IReadOnlyList<AttackVolley> pendingVolleys) {
            if (pendingVolleys == null || pendingVolleys.Count == 0) {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < pendingVolleys.Count; i++) {
                var volley = pendingVolleys[i];
                if (volley == null) {
                    continue;
                }

                var dice = volley.Dice;
                for (var j = 0; j < dice.Count; j++) {
                    if (dice[j].Kind == DiceKind.Jumbo) {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
