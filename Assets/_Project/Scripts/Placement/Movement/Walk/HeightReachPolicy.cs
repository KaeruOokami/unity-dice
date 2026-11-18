using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class HeightReachPolicy
    {
        const float SurfaceYEpsilon = 0.001f;

        public static bool CanStepBetweenNorm(float fromNorm, float toNorm, float maxStepNorm) {
            return Mathf.Abs(fromNorm - toNorm) <= maxStepNorm;
        }

        public static bool CanDescendToNorm(float footingNorm, float targetNorm, float maxStepNorm) {
            return footingNorm + SurfaceYEpsilon >= targetNorm
                && footingNorm - targetNorm <= maxStepNorm;
        }

        /// <summary>
        /// Standing surface Y → landing Y step (no footing substitution).
        /// Used for erasing-standing leave so Top→Bottom is not collapsed to Bottom→Bottom.
        /// </summary>
        public static bool CanWalkBySurfaceDelta(
            float fromSurfaceWorldY,
            float targetSurfaceWorldY,
            HeightReachEvaluation reach) {
            var fromNorm = NormalizedHeight.ToNormalized(
                fromSurfaceWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            var targetNorm = NormalizedHeight.ToNormalized(
                targetSurfaceWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            return CanStepBetweenNorm(fromNorm, targetNorm, reach.GetMaxStepNorm());
        }

        public static float GetSurfaceDeltaNorm(
            float fromSurfaceWorldY,
            float targetSurfaceWorldY,
            HeightReachEvaluation reach) {
            var fromNorm = NormalizedHeight.ToNormalized(
                fromSurfaceWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            var targetNorm = NormalizedHeight.ToNormalized(
                targetSurfaceWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            return Mathf.Abs(fromNorm - targetNorm);
        }

        /// <summary>
        /// Erasing standing descent whose real surface gap exceeds walk max → dissolve hold
        /// (Floor and Bottom use the same predicate).
        /// </summary>
        public static bool NeedsErasingDissolveDescent(
            BoardSurface fromSurface,
            float targetSurfaceWorldY,
            HeightReachEvaluation reach) {
            if (!fromSurface.IsErasingStandingSurface) {
                return false;
            }

            if (targetSurfaceWorldY >= fromSurface.SurfaceWorldY - SurfaceYEpsilon) {
                return false;
            }

            return !CanWalkBySurfaceDelta(
                fromSurface.SurfaceWorldY,
                targetSurfaceWorldY,
                reach);
        }

        public static bool CanTransfer(
            BoardSurface fromSurface,
            float targetSurfaceWorldY,
            DiceController standingDice,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            bool allowDescentOnly,
            DiceController transferTarget = null) {
            if (transferTarget != null && transferTarget.AllowsUnconditionalMount) {
                return true;
            }

            var footingWorldY = TransferFootingPolicy.GetFootingWorldY(
                fromSurface,
                targetSurfaceWorldY,
                standingDice,
                registry);
            var standingNorm = NormalizedHeight.ToNormalized(
                fromSurface.SurfaceWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            var targetNorm = NormalizedHeight.ToNormalized(
                targetSurfaceWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            var footingNorm = NormalizedHeight.ToNormalized(
                footingWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            var maxStepNorm = reach.GetMaxStepNorm();

            if (allowDescentOnly && targetNorm < standingNorm - SurfaceYEpsilon) {
                return CanDescendToNorm(footingNorm, targetNorm, maxStepNorm);
            }

            return CanStepBetweenNorm(footingNorm, targetNorm, maxStepNorm);
        }

        public static float GetTransferDeltaNorm(
            BoardSurface fromSurface,
            float targetSurfaceWorldY,
            DiceController standingDice,
            DiceRegistry registry,
            HeightReachEvaluation reach) {
            var footingWorldY = TransferFootingPolicy.GetFootingWorldY(
                fromSurface,
                targetSurfaceWorldY,
                standingDice,
                registry);
            var footingNorm = NormalizedHeight.ToNormalized(
                footingWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            var targetNorm = NormalizedHeight.ToNormalized(
                targetSurfaceWorldY,
                reach.FloorWorldY,
                reach.CellSize);
            return Mathf.Abs(footingNorm - targetNorm);
        }
    }
}
