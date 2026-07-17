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
            bool isJumping,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            if (!JumpPlayerTransferPolicy.CanUsePlayerOnlyLowerLevelJump(isJumping, standingDice)
                || standingDice == null
                || !JumpPlayerTransferPolicy.IsLowerLevelTransfer(fromLevel, SurfaceHeightLevel.Bottom)) {
                return false;
            }

            if (SurfaceHeightLevel.ToDiceStackTier(fromLevel) != standingDice.CurrentState.Tier) {
                return false;
            }

            if (!registry.TryGetBottomAt(fromCell, out var landingDice)
                || landingDice == null
                || landingDice == standingDice
                || GhostPlacementRules.IsPlayerPassThrough(landingDice)) {
                return false;
            }

            // Player-only jump descent: no step-height check.
            transition = MovementTransition.Walkable(
                landingDice,
                SurfaceHeightLevel.Bottom,
                MovementTransitionRoute.HeightTransfer);
            return true;
        }
    }
}
