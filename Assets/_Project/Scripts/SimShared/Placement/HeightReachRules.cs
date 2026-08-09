namespace DiceGame.SimShared.Placement
{
    /// <summary>
    /// Pure height-step reach matching <c>HeightReachPolicy.CanStepBetweenNorm</c>.
    /// </summary>
    public static class HeightReachRules
    {
        public const float SurfaceNormEpsilon = 0.001f;

        public static bool CanStepBetweenNorm(float fromNorm, float toNorm, float maxStepNorm)
        {
            var delta = fromNorm - toNorm;
            if (delta < 0f)
            {
                delta = -delta;
            }

            return delta <= maxStepNorm + SurfaceNormEpsilon;
        }

        public static bool CanStepBetweenNormPermille(int fromNorm, int toNorm, int maxStepPermille)
        {
            var delta = fromNorm - toNorm;
            if (delta < 0)
            {
                delta = -delta;
            }

            return delta * 1000 <= maxStepPermille;
        }
    }
}
