using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class JumpGridRollPolicy
    {
        public static bool TryCreateCoupledTransition(
            Vector2Int fromCell,
            Vector2Int toCell,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            GridMovePlanBuilder planBuilder,
            out MovementTransition transition) {
            transition = default;

            if (standingDice == null || !fromSurface.AllowsRoll) {
                return false;
            }

            if (SurfaceHeightLevel.ToDiceStackTier(fromSurface.Level) != standingDice.CurrentState.Tier) {
                return false;
            }

            var distance = MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell);
            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance) {
                return false;
            }

            // For Ice dice, forbid 2-cell jump movement (distance > 1).
            // 1-cell jump is still allowed; orientation will remain unchanged via DiceGridMovePlanner.
            if (standingDice.Kind == DiceKind.Ice
                && distance > 1) {
                return false;
            }

            if (fromCell + direction.ToGridDelta() * distance != toCell) {
                return false;
            }

            if (!planBuilder.TryBuild(
                standingDice.CurrentState,
                direction,
                distance,
                context,
                out var plan,
                out _)) {
                return false;
            }

            var targetLevel = SurfaceHeightLevel.FromDiceStackTier(plan.To.Tier);
            transition = MovementTransition.WalkableWithGridPlan(
                standingDice,
                targetLevel,
                MovementTransitionRoute.CoupledGridMove,
                plan);
            return true;
        }
    }
}
