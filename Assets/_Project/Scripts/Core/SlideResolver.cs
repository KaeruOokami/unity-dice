using DiceGame.Core;
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
        public static bool TrySlideBottom(DiceState state, Direction direction, IBoard board, out DiceState nextState) {
            nextState = default;
            if (state.Tier != DiceStackTier.Bottom) {
                return false;
            }

            var targetPos = state.GridPos + direction.ToGridDelta();
            if (!board.CanPlaceBottomDiceAt(targetPos)) {
                return false;
            }

            nextState = new DiceState(targetPos, state.Orientation, DiceStackTier.Bottom);
            return true;
        }

        public static bool TrySlideTop(
            DiceState state,
            Direction direction,
            IBoard board,
            out DiceState nextState,
            out TopSlideResult result) {
            nextState = default;
            result = default;

            if (state.Tier != DiceStackTier.Top) {
                return false;
            }

            var targetPos = state.GridPos + direction.ToGridDelta();
            if (board.CanPlaceTopDiceAt(targetPos)) {
                nextState = new DiceState(targetPos, state.Orientation, DiceStackTier.Top);
                result = TopSlideResult.Parallel;
                return true;
            }

            if (board.CanPlaceBottomDiceAt(targetPos)) {
                nextState = new DiceState(targetPos, state.Orientation, DiceStackTier.Bottom);
                result = TopSlideResult.FallToBottom;
                return true;
            }

            return false;
        }

        public static bool TrySlide(DiceState state, Direction direction, IBoard board, out DiceState nextState) {
            if (state.Tier == DiceStackTier.Top) {
                return TrySlideTop(state, direction, board, out nextState, out _);
            }

            return TrySlideBottom(state, direction, board, out nextState);
        }
    }
}
