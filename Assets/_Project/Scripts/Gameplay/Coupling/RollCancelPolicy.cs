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

            if (InputDirection.TryFromVector2(input, out var inputDir)
                && inputDir == activePlan.Direction.Opposite()) {
                return RollCancelKind.Reverse;
            }

            return RollCancelKind.None;
        }
    }
}
