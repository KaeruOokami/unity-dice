using System.Collections.Generic;
using DiceGame.Core;

namespace DiceGame.Gameplay
{
    public static class DiceOneVanishTrigger
    {
        public static bool ShouldTrigger(IReadOnlyList<DiceController> allDice) {
            if (allDice == null) {
                return false;
            }

            foreach (var dissolving in allDice) {
                if (dissolving == null || !dissolving.IsDissolving) {
                    continue;
                }

                var dissolvingSlot = DiceSlot.FromDice(dissolving);
                foreach (var candidate in allDice) {
                    if (candidate == null || candidate.IsSpawning) {
                        continue;
                    }

                    if (candidate.CurrentState.Orientation.Top != 1) {
                        continue;
                    }

                    if (DiceStackAdjacency.IsAdjacentForMatch(dissolvingSlot, DiceSlot.FromDice(candidate))) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
