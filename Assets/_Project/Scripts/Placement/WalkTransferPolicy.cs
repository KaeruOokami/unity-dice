using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class WalkTransferPolicy
    {
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
            out MovementTransition transition) {
            transition = default;
            if (target == null) {
                return false;
            }

            if (!targetSurface.AllowsWalkFrom(fromSurface, isJumping)) {
                return false;
            }

            if (standingTier == DiceStackTier.Bottom
                && target.CurrentState.Tier == DiceStackTier.Bottom
                && registry.HasTopAt(target.CurrentState.GridPos)) {
                return false;
            }

            if (!HeightReachPolicy.CanStepBetween(
                reachY,
                target.GetLogicalTopSurfaceWorldY(),
                maxStepHeight)) {
                return false;
            }

            var targetLayer = target.CurrentState.Tier == DiceStackTier.Top
                ? SurfaceLayer.Top
                : SurfaceLayer.Bottom;
            transition = MovementTransition.Walkable(
                target,
                targetLayer,
                MovementTransitionRoute.HeightTransfer);
            return true;
        }
    }
}
