using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Core
{
    public static class SlideResolver
    {
        public static bool TrySlide(DiceState state, Direction direction, IBoard board, out DiceState nextState) {
            nextState = default;
            var targetPos = state.GridPos + direction.ToGridDelta();

            if (!board.CanDiceRollInto(targetPos)) {
                return false;
            }

            nextState = new DiceState(targetPos, state.Orientation);
            return true;
        }
    }
}
