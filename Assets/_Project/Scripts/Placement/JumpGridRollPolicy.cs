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
            DiceStackTier standingTier,
            Direction direction,
            PassabilityContext context,
            GridMovePlanBuilder planBuilder,
            out MovementTransition transition) {
            transition = default;

            if (standingDice == null || !fromSurface.AllowsRoll) {
                return false;
            }

            if (standingTier != standingDice.CurrentState.Tier) {
                return false;
            }

            var distance = MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell);
            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance) {
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

            var layer = plan.To.Tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            transition = MovementTransition.WalkableWithGridPlan(
                standingDice,
                layer,
                MovementTransitionRoute.CoupledGridMove,
                plan);
            return true;
        }
    }
}
