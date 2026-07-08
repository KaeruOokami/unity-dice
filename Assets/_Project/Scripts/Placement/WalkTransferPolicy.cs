using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class WalkTransferPolicy
    {
        public static bool IsLandingTierAtOrBelowStandingTier(
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
                    SurfaceLayer.Floor,
                    MovementTransitionRoute.FloorTransfer);
            }

            if (fromSurface.IsDissolving && fromSurface.Layer == SurfaceLayer.Bottom) {
                return MovementTransition.BlockedStepOnly(null, SurfaceLayer.Floor);
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
            if (bottomDice == null) {
                return false;
            }

            if (!HeightReachPolicy.CanTransfer(
                fromSurface,
                bottomDice.GetLogicalTopSurfaceWorldY(),
                null,
                registry,
                reach,
                allowDescentOnly: false)) {
                return false;
            }

            transition = MovementTransition.Walkable(
                bottomDice,
                SurfaceLayer.Bottom,
                MovementTransitionRoute.HeightTransfer);
            return true;
        }

        public static bool TryEvaluateDiceToDice(
            DiceController target,
            DiceStackTier standingTier,
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
            if (!TryValidateDiceToDiceTarget(
                target,
                standingTier,
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
                allowDescentOnly)) {
                transition = CreateWalkableTransfer(target);
                return true;
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
            DiceStackTier standingTier,
            DiceRegistry registry,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            DiceController standingDice,
            HeightReachEvaluation reach,
            out MovementTransition transition,
            out string rejectReason) {
            transition = default;
            rejectReason = null;
            if (!TryValidateDiceToDiceTarget(
                target,
                standingTier,
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

            if (!targetSurface.AllowsWalkFrom(fromSurface, isJumping)) {
                rejectReason = "allows-walk-from-false";
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
            if (!fromSurface.IsDissolving) {
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

            var targetLayer = target.CurrentState.Tier == DiceStackTier.Top
                ? SurfaceLayer.Top
                : SurfaceLayer.Bottom;
            transition = MovementTransition.BlockedStepOnly(target, targetLayer);
            return true;
        }

        static MovementTransition CreateWalkableTransfer(DiceController target) {
            var targetLayer = target.CurrentState.Tier == DiceStackTier.Top
                ? SurfaceLayer.Top
                : SurfaceLayer.Bottom;
            return MovementTransition.Walkable(
                target,
                targetLayer,
                MovementTransitionRoute.HeightTransfer);
        }
    }
}
