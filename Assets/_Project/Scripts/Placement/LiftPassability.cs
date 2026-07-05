using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class LiftPassability
    {
        public static bool CanLift(
            CharacterPlacement standing,
            bool isOnFloor,
            DiceController standingDice,
            DiceController dice,
            DiceRegistry registry) {
            if (dice == null || registry == null) {
                return false;
            }

            if (standingDice != null && dice == standingDice) {
                return false;
            }

            if (!dice.Capabilities.CanBeLiftedByPlayer) {
                return false;
            }

            if (!IsReachable(standing, dice)) {
                return false;
            }

            if (isOnFloor) {
                if (dice.CurrentState.Tier == DiceStackTier.Top) {
                    return true;
                }

                return dice.CurrentState.Tier == DiceStackTier.Bottom
                    && !registry.HasTopAt(dice.CurrentState.GridPos);
            }

            if (standing.Tier == DiceStackTier.Bottom) {
                return true;
            }

            return dice.CurrentState.Tier == DiceStackTier.Top;
        }

        public static bool IsReachable(CharacterPlacement standing, DiceController dice) {
            if (dice == null) {
                return false;
            }

            var playerTier = standing.IsOnFloor ? DiceStackTier.Bottom : standing.Tier;
            var playerSlot = new DiceSlot(standing.GridCell, playerTier);
            return DiceStackAdjacency.IsAdjacentForLift(playerSlot, DiceSlot.FromDice(dice));
        }
    }
}
