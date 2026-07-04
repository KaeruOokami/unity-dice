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
        public static bool IsCancelEligiblePlan(DiceGridMovePlan plan) {
            return plan.Kind is DiceGridMoveKind.Parallel or DiceGridMoveKind.Demote;
        }

        public static RollCancelKind Evaluate(
            DiceGridMovePlan activePlan,
            float rollProgress,
            float windowProgress,
            Vector2 input,
            bool jumpPressed,
            bool wasGroundRoll) {
            if (!IsCancelEligiblePlan(activePlan)) {
                return RollCancelKind.None;
            }

            if (rollProgress > windowProgress) {
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
