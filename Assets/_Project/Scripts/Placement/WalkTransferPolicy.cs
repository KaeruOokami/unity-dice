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
            float effectiveReachY,
            float floorSurfaceY,
            float maxStepHeight,
            BoardSurface fromSurface) {
            if (HeightReachPolicy.CanStepBetween(effectiveReachY, floorSurfaceY, maxStepHeight)) {
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
            float reachY,
            DiceController bottomDice,
            float maxStepHeight,
            out MovementTransition transition) {
            transition = default;
            if (bottomDice == null) {
                return false;
            }

            if (!HeightReachPolicy.CanStepBetween(
                reachY,
                bottomDice.GetLogicalTopSurfaceWorldY(),
                maxStepHeight)) {
                return false;
            }

            transition = MovementTransition.Walkable(
                bottomDice,
                SurfaceLayer.Bottom,
                MovementTransitionRoute.HeightTransfer);
            return true;
        }

        public static bool TryEvaluateDiceToDice(
            float reachY,
            DiceController target,
            DiceStackTier standingTier,
            DiceRegistry registry,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            bool isJumping,
            float maxStepHeight,
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

            var targetSurfaceY = targetSurface.SurfaceWorldY;
            if (HeightReachPolicy.CanStepBetween(reachY, targetSurfaceY, maxStepHeight)) {
                transition = CreateWalkableTransfer(target);
                return true;
            }

            if (TryCreateDissolveDescentHold(
                reachY,
                target,
                standingTier,
                fromSurface,
                targetSurface,
                maxStepHeight,
                out transition)) {
                return true;
            }

            var delta = UnityEngine.Mathf.Abs(reachY - targetSurfaceY);
            rejectReason =
                $"step-height reachY={reachY:F3} targetY={targetSurfaceY:F3} " +
                $"max={maxStepHeight:F3} delta={delta:F3}";
            return false;
        }

        public static bool TryEvaluateDissolveDescentHold(
            float reachY,
            DiceController target,
            DiceStackTier standingTier,
            DiceRegistry registry,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            float maxStepHeight,
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
                reachY,
                target,
                standingTier,
                fromSurface,
                targetSurface,
                maxStepHeight,
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
            float reachY,
            DiceController target,
            DiceStackTier standingTier,
            BoardSurface fromSurface,
            BoardSurface targetSurface,
            float maxStepHeight,
            out MovementTransition transition) {
            transition = default;
            if (!fromSurface.IsDissolving) {
                return false;
            }

            if (!IsLandingTierAtOrBelowStandingTier(standingTier, target.CurrentState.Tier)) {
                return false;
            }

            var targetSurfaceY = targetSurface.SurfaceWorldY;
            if (HeightReachPolicy.CanStepBetween(reachY, targetSurfaceY, maxStepHeight)) {
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
