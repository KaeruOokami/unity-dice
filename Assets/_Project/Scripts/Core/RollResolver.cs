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

            if (state.Tier == DiceStackTier.Bottom) {
                if (hasTopOnSameCell) {
                    return false;
                }

                var targetPos = state.GridPos + direction.ToGridDelta();
                if (!placement.CanDiceRollInto(targetPos)) {
                    return false;
                }

                return TryBuildRolledState(state, targetPos, state.Tier, direction, out nextState);
            }

            if (state.Tier == DiceStackTier.Top) {
                if (!SlideResolver.TrySlideTop(state, direction, placement, out var slideState, out var result)
                    || result != TopSlideResult.Parallel) {
                    return false;
                }

                return TryBuildRolledState(state, slideState.GridPos, DiceStackTier.Top, direction, out nextState);
            }

            return false;
        }

        static bool TryBuildRolledState(
            DiceState state,
            Vector2Int targetPos,
            DiceStackTier tier,
            Direction direction,
            out DiceState nextState) {
            nextState = default;

            var nextOrientation = state.Orientation.Roll(direction);
            if (!nextOrientation.IsValid()) {
                return false;
            }

            nextState = new DiceState(targetPos, nextOrientation, tier);
            return true;
        }
    }
}
