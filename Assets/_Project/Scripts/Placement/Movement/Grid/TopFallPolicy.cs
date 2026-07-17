using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class TopFallPolicy
    {
        public static bool TryEvaluate(
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            GridMovePlanBuilder planBuilder,
            out MovementTransition transition) {
            transition = default;

            if (fromLevel != SurfaceHeightLevel.Top
                || standingDice == null
                || !fromSurface.AllowsRoll
                || standingDice.CurrentState.Tier != DiceStackTier.Top) {
                return false;
            }

            if (!planBuilder.TryBuild(
                standingDice.CurrentState,
                direction,
                1,
                context,
                out var plan,
                out _)
                || plan.Kind != DiceGridMoveKind.Demote) {
                return false;
            }

            transition = MovementTransition.WalkableWithGridPlan(
                standingDice,
                SurfaceHeightLevel.Bottom,
                MovementTransitionRoute.TopFall,
                plan);
            return true;
        }
    }
}
