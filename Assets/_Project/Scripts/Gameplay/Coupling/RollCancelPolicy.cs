using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay.Coupling
{
    public enum RollCancelKind
    {
        None,
        Reverse,
        SwitchToJump
    }

    public static class RollCancelPolicy
    {
        public static RollCancelKind Evaluate(
            DiceGridMovePlan activePlan,
            float elapsedSeconds,
            float windowDuration,
            Vector2 input,
            bool jumpPressed,
            bool wasGroundRoll) {
            if (activePlan.Kind != DiceGridMoveKind.Parallel) {
                return RollCancelKind.None;
            }

            if (elapsedSeconds > windowDuration) {
                return RollCancelKind.None;
            }

            if (jumpPressed && wasGroundRoll) {
                return RollCancelKind.SwitchToJump;
            }

            if (TryInputToDirection(input, out var inputDir)
                && inputDir == activePlan.Direction.Opposite()) {
                return RollCancelKind.Reverse;
            }

            return RollCancelKind.None;
        }

        static bool TryInputToDirection(Vector2 input, out Direction direction) {
            direction = Direction.North;
            if (input.sqrMagnitude <= 0f) {
                return false;
            }

            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y)) {
                direction = input.x >= 0f ? Direction.East : Direction.West;
            } else {
                direction = input.y >= 0f ? Direction.North : Direction.South;
            }

            return true;
        }
    }
}
