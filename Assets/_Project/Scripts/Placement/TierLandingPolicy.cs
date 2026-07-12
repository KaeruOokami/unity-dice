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
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            PassabilityContext context,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;

            if (MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell) != 1) {
                return false;
            }

            if (!context.IsJumping
                || fromLevel != SurfaceHeightLevel.Bottom
                || standingDice == null
                || !fromSurface.AllowsRoll) {
                return false;
            }

            if (!context.AllowJumpTierChange) {
                return false;
            }

            if (!registry.TryGetTopAt(toCell, out var topDice)
                || topDice == null
                || (topDice.IsRadianceErasing && !standingDice.IsRadianceErasing)
                || !HeightReachPolicy.CanTransfer(
                    fromSurface,
                    topDice.GetLogicalTopSurfaceWorldY(),
                    standingDice,
                    registry,
                    reach,
                    allowDescentOnly: false,
                    topDice)) {
                return false;
            }

            transition = MovementTransition.Walkable(
                topDice,
                SurfaceHeightLevel.Top,
                MovementTransitionRoute.TierLanding);
            return true;
        }
    }
}
