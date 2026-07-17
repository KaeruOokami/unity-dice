using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class WalkTransferPolicy
    {
        const float SurfaceYEpsilon = 0.001f;

        static bool IsSinkErasingDescentTransfer(BoardSurface fromSurface, float targetSurfaceWorldY) {
            return fromSurface.IsSinkErasing
                && targetSurfaceWorldY < fromSurface.SurfaceWorldY - SurfaceYEpsilon;
        }

        static bool IsLandingTierAtOrBelowStandingTier(
            DiceStackTier standingTier,
            DiceStackTier landingTier) {
            if (standingTier == DiceStackTier.Bottom) {
                return landingTier == DiceStackTier.Bottom;
            }

            return landingTier == DiceStackTier.Top || landingTier == DiceStackTier.Bottom;
        }

        public static MovementTransition EvaluateFloor(
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            bool allowDescentOnly = false) {
            if (HeightReachPolicy.CanTransfer(
                fromSurface,
                reach.FloorWorldY,
                standingDice,
                registry,
                reach,
                allowDescentOnly)) {
                return MovementTransition.Walkable(
                    null,
                    SurfaceHeightLevel.Floor,
                    MovementTransitionRoute.FloorTransfer);
            }

            if (fromSurface.IsSinkErasing && fromSurface.Level == SurfaceHeightLevel.Bottom) {
                return MovementTransition.BlockedStepOnly(null, SurfaceHeightLevel.Floor);
            }

            return MovementTransition.Blocked();
        }

        public static bool TryEvaluateFloorToBottom(
            BoardSurface fromSurface,
            DiceController bottomDice,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            if (bottomDice == null || GhostPlacementRules.IsPlayerPassThrough(bottomDice)) {
                return false;
            }

            if (!HeightReachPolicy.CanTransfer(
                fromSurface,
                bottomDice.GetLogicalTopSurfaceWorldY(),
                null,
                registry,
                reach,
                allowDescentOnly: false,
                bottomDice)) {
                return false;
            }

            transition = MovementTransition.Walkable(
                bottomDice,
                SurfaceHeightLevel.Bottom,
                MovementTransitionRoute.HeightTransfer);
            return true;
        }

        public static bool TryEvaluateDiceToDice(
            DiceController target,
            int fromLevel,
            DiceRegistry registry,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            DiceController standingDice,
            bool isJumping,
            HeightReachEvaluation reach,
            bool allowDescentOnly,
            out MovementTransition transition,
            out string rejectReason) {
            transition = default;
            rejectReason = null;
            var standingTier = SurfaceHeightLevel.ToDiceStackTier(fromLevel);
            if (!TryValidateDiceToDiceTarget(
                target,
                standingTier,
                standingDice,
                registry,
                fromSurface,
                targetSurface,
                isJumping,
                out rejectReason)) {
                return false;
            }

            if (HeightReachPolicy.CanTransfer(
                fromSurface,
                targetSurface.SurfaceWorldY,
                standingDice,
                registry,
                reach,
                allowDescentOnly,
                target)) {
                transition = CreateWalkableTransfer(target);
                return true;
            }

            if (!isJumping
                && TryCreateDissolveDescentHold(
                target,
                standingTier,
                fromSurface,
                targetSurface,
                standingDice,
                registry,
                reach,
                out transition)) {
                return true;
            }

            var deltaNorm = HeightReachPolicy.GetTransferDeltaNorm(
                fromSurface,
                targetSurface.SurfaceWorldY,
                standingDice,
                registry,
                reach);
            rejectReason =
                $"step-height targetY={targetSurface.SurfaceWorldY:F3} " +
                $"maxNorm={reach.GetMaxStepNorm():F3} deltaNorm={deltaNorm:F3}";
            return false;
        }

        public static bool TryEvaluateDissolveDescentHold(
            DiceController target,
            int fromLevel,
            DiceRegistry registry,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            DiceController standingDice,
            HeightReachEvaluation reach,
            out MovementTransition transition,
            out string rejectReason) {
            transition = default;
            rejectReason = null;
            var standingTier = SurfaceHeightLevel.ToDiceStackTier(fromLevel);
            if (!TryValidateDiceToDiceTarget(
                target,
                standingTier,
                standingDice,
                registry,
                fromSurface,
                targetSurface,
                isJumping: false,
                out rejectReason)) {
                return false;
            }

            if (TryCreateDissolveDescentHold(
                target,
                standingTier,
                fromSurface,
                targetSurface,
                standingDice,
                registry,
                reach,
                out transition)) {
                return true;
            }

            rejectReason = "dissolve-descent-hold-unavailable";
            return false;
        }

        static bool TryValidateDiceToDiceTarget(
            DiceController target,
            DiceStackTier standingTier,
            DiceController standingDice,
            DiceRegistry registry,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            bool isJumping,
            out string rejectReason) {
            rejectReason = null;
            if (target == null) {
                rejectReason = "target-null";
                return false;
            }

            if (GhostPlacementRules.IsPlayerPassThrough(target)) {
                rejectReason = "ghost-pass-through";
                return false;
            }

            if (!targetSurface.AllowsWalkFrom(fromSurface, isJumping)) {
                rejectReason = "allows-walk-from-false";
                return false;
            }

            if (target.IsRadianceErasing
                && (standingDice == null || !standingDice.IsRadianceErasing)) {
                rejectReason = "radiance-top-transfer-blocked";
                return false;
            }

            if (standingTier == DiceStackTier.Bottom
                && target.CurrentState.Tier == DiceStackTier.Bottom
                && registry.HasTopAt(target.CurrentState.GridPos)) {
                rejectReason = "neighbor-bottom-occluded-by-top";
                return false;
            }

            if (!IsLandingTierAtOrBelowStandingTier(standingTier, target.CurrentState.Tier)) {
                rejectReason =
                    $"landing-tier-above-standing standingTier={standingTier} landingTier={target.CurrentState.Tier}";
                return false;
            }

            return true;
        }

        static bool TryCreateDissolveDescentHold(
            DiceController target,
            DiceStackTier standingTier,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            DiceController standingDice,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            if (!fromSurface.IsSinkErasing) {
                return false;
            }

            if (!IsSinkErasingDescentTransfer(fromSurface, targetSurface.SurfaceWorldY)) {
                return false;
            }

            if (!IsLandingTierAtOrBelowStandingTier(standingTier, target.CurrentState.Tier)) {
                return false;
            }

            if (HeightReachPolicy.CanTransfer(
                fromSurface,
                targetSurface.SurfaceWorldY,
                standingDice,
                registry,
                reach,
                allowDescentOnly: false)) {
                return false;
            }

            var targetLevel = SurfaceHeightLevel.FromDiceStackTier(target.CurrentState.Tier);
            transition = MovementTransition.BlockedStepOnly(target, targetLevel);
            return true;
        }

        static MovementTransition CreateWalkableTransfer(DiceController target) {
            var targetLevel = SurfaceHeightLevel.FromDiceStackTier(target.CurrentState.Tier);
            return MovementTransition.Walkable(
                target,
                targetLevel,
                MovementTransitionRoute.HeightTransfer);
        }
    }
}
