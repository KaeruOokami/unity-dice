using System.Collections.Generic;
using DiceGame.Core;

namespace DiceGame.Gameplay
{
    public static class DiceOneVanishTrigger
    {
        public static bool ShouldTrigger(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice) {
            if (allDice == null || actionDice == null || actionDice.Count == 0) {
                return false;
            }

            foreach (var one in actionDice) {
                if (one == null || one.IsSpawning) {
                    continue;
                }

                if (one.CurrentState.Orientation.Top != 1) {
                    continue;
                }

                var oneSlot = DiceSlot.FromDice(one);

                foreach (var erasing in allDice) {
                    if (erasing == null || !erasing.IsErasing) {
                        continue;
                    }

                    if (!DiceStackAdjacency.IsAdjacentForMatch(DiceSlot.FromDice(erasing), oneSlot)) {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
