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

        public static bool CanTransfer(
            BoardSurface fromSurface,
            float targetSurfaceWorldY,
            DiceController standingDice,
            DiceRegistry registry,
            HeightReachEvaluation reach,
            bool allowDescentOnly) {
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
