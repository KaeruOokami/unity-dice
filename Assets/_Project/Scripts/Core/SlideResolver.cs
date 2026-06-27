using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Core
{
    public enum TopSlideResult
    {
        Parallel,
        FallToBottom
    }

    public static class SlideResolver
    {
        public static bool TrySlideBottom(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            out DiceState nextState) {
            nextState = default;
            if (state.Tier != DiceStackTier.Bottom) {
                return false;
            }

            var targetPos = state.GridPos + direction.ToGridDelta();
            if (!placement.CanPlaceBottomDiceAt(targetPos)) {
                return false;
            }

            nextState = new DiceState(targetPos, state.Orientation, DiceStackTier.Bottom);
            return true;
        }

        public static bool TryStackOntoAdjacentBottom(
            DiceState state,
            Vector2Int toCell,
            IDicePlacement placement,
            out DiceState nextState) {
            nextState = default;
            if (state.Tier != DiceStackTier.Bottom) {
                return false;
            }

            var delta = toCell - state.GridPos;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) {
                return false;
            }

            if (!placement.CanPlaceTopDiceAt(toCell)) {
                return false;
            }

            nextState = new DiceState(toCell, state.Orientation, DiceStackTier.Top);
            return true;
        }

        public static bool TrySlideTop(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            out DiceState nextState,
            out TopSlideResult result) {
            nextState = default;
            result = default;

            if (state.Tier != DiceStackTier.Top) {
                return false;
            }

            var targetPos = state.GridPos + direction.ToGridDelta();
            if (placement.CanPlaceTopDiceAt(targetPos)) {
                nextState = new DiceState(targetPos, state.Orientation, DiceStackTier.Top);
                result = TopSlideResult.Parallel;
                return true;
            }

            if (placement.CanPlaceBottomDiceAt(targetPos)) {
                nextState = new DiceState(targetPos, state.Orientation, DiceStackTier.Bottom);
                result = TopSlideResult.FallToBottom;
                return true;
            }

            return false;
        }

        public static bool TrySlide(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            out DiceState nextState) {
            if (state.Tier == DiceStackTier.Top) {
                return TrySlideTop(state, direction, placement, out nextState, out _);
            }

            return TrySlideBottom(state, direction, placement, out nextState);
        }
    }
}
