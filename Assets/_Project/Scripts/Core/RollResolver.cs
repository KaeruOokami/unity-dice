using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Core
{
    public static class RollResolver
    {
        public static bool TryRoll(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            out DiceState nextState) {
            nextState = default;

            if (state.Tier != DiceStackTier.Bottom || hasTopOnSameCell) {
                return false;
            }

            var targetPos = state.GridPos + direction.ToGridDelta();
            if (!placement.CanDiceRollInto(targetPos)) {
                return false;
            }

            var nextOrientation = state.Orientation.Roll(direction);
            if (!nextOrientation.IsValid()) {
                return false;
            }

            nextState = new DiceState(targetPos, nextOrientation, DiceStackTier.Bottom);
            return true;
        }
    }
}
