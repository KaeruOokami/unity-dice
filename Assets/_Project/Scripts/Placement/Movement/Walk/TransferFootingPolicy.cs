using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class TransferFootingPolicy
    {
        const float SurfaceYEpsilon = 0.001f;

        public static float GetFootingWorldY(
            BoardSurface fromSurface,
            float targetSurfaceWorldY,
            DiceController standingDice,
            DiceRegistry registry) {
            if (standingDice == null
                || targetSurfaceWorldY >= fromSurface.SurfaceWorldY - SurfaceYEpsilon) {
                return fromSurface.SurfaceWorldY;
            }

            var support = registry.ResolveSupportBottom(standingDice);
            if (support != null) {
                return support.GetLogicalTopSurfaceWorldY();
            }

            return fromSurface.SurfaceWorldY;
        }
    }
}
