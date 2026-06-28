using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Core
{
    public static class RollResolver
    {
        public const int MaxParallelRollDistance = 2;

        public static bool TryRoll(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            out DiceState nextState) {
            return TryRollDistance(state, direction, placement, hasTopOnSameCell, 1, out nextState);
        }

        public static bool TryRollDistance(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            int distance,
            out DiceState nextState) {
            nextState = default;

            if (distance < 1 || distance > MaxParallelRollDistance) {
                return false;
            }

            var rollingState = state;
            for (var step = 0; step < distance; step++) {
                var hasTopOnCell = step == 0 && hasTopOnSameCell;

                if (rollingState.Tier == DiceStackTier.Bottom) {
                    if (hasTopOnCell) {
                        return false;
                    }

                    var targetPos = rollingState.GridPos + direction.ToGridDelta();
                    if (!placement.CanDiceRollInto(targetPos)) {
                        return false;
                    }

                    if (!TryBuildRolledState(rollingState, targetPos, rollingState.Tier, direction, out rollingState)) {
                        return false;
                    }

                    continue;
                }

                if (rollingState.Tier == DiceStackTier.Top) {
                    if (!SlideResolver.TrySlideTop(rollingState, direction, placement, out var slideState, out var result)
                        || result != TopSlideResult.Parallel) {
                        return false;
                    }

                    if (!TryBuildRolledState(rollingState, slideState.GridPos, DiceStackTier.Top, direction, out rollingState)) {
                        return false;
                    }

                    continue;
                }

                return false;
            }

            nextState = rollingState;
            return true;
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
