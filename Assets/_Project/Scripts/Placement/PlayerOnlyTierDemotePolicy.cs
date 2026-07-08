using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class PlayerOnlyTierDemotePolicy
    {
        public static bool TryEvaluate(
            Vector2Int fromCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceStackTier standingTier,
            bool isJumping,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            if (!JumpPlayerTransferPolicy.UsesPlayerOnlyReach(isJumping, standingDice)
                || standingDice == null
                || fromLevel != SurfaceHeightLevel.Top) {
                return false;
            }

            DiceController landingDice;
            if (standingTier == DiceStackTier.Top) {
                if (standingDice.CurrentState.Tier != DiceStackTier.Top) {
                    return false;
                }

                if (!registry.TryGetBottomAt(fromCell, out landingDice)
                    || landingDice == null
                    || landingDice == standingDice) {
                    return false;
                }
            } else {
                if (standingDice.CurrentState.Tier != DiceStackTier.Bottom) {
                    return false;
                }

                landingDice = standingDice;
            }

            var targetSurface = BoardSurface.FromDice(fromCell, SurfaceHeightLevel.Bottom, landingDice);
            if (!HeightReachPolicy.CanTransfer(
                fromSurface,
                targetSurface.SurfaceWorldY,
                standingDice,
                registry,
                reach,
                allowDescentOnly: true)) {
                return false;
            }

            transition = MovementTransition.Walkable(
                landingDice,
                SurfaceHeightLevel.Bottom,
                MovementTransitionRoute.HeightTransfer);
            return true;
        }
    }
}
