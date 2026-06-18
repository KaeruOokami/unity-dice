using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Core
{
    public static class RollResolver
    {
        public static bool TryRoll(DiceState state, Direction direction, IBoard board, out DiceState nextState) {
            nextState = default;
            var targetPos = state.GridPos + direction.ToGridDelta();

            if (!board.CanDiceRollInto(targetPos)) {
                return false;
            }

            var nextOrientation = state.Orientation.Roll(direction);
            if (!nextOrientation.IsValid()) {
                return false;
            }

            nextState = new DiceState(targetPos, nextOrientation);
            return true;
        }
    }
}
