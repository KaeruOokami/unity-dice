using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class TierLandingPolicy
    {
        public static bool TryEvaluate(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceStackTier standingTier,
            PassabilityContext context,
            DiceRegistry registry,
            float maxStepHeight,
            out MovementTransition transition) {
            transition = default;

            if (MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell) != 1) {
                return false;
            }

            if (!context.IsJumping
                || fromLayer != SurfaceLayer.Bottom
                || standingTier != DiceStackTier.Bottom
                || standingDice == null
                || !fromSurface.AllowsRoll) {
                return false;
            }

            if (!context.AllowJumpTierChange) {
                return false;
            }

            var reachY = context.EffectiveReachY;
            if (!registry.TryGetTopAt(toCell, out var topDice)
                || topDice == null
                || !HeightReachPolicy.CanStepBetween(reachY, topDice.GetLogicalTopSurfaceWorldY(), maxStepHeight)) {
                return false;
            }

            transition = MovementTransition.Walkable(
                topDice,
                SurfaceLayer.Top,
                MovementTransitionRoute.TierLanding);
            return true;
        }
    }
}
