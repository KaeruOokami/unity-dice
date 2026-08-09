namespace DiceGame.Core
{
    /// <summary>
    /// Jumbo 2x2 footprint cell math without UnityEngine types.
    /// </summary>
    public static class JumboFootprintCells
    {
        public const int Size = 2;
        public const int CellCount = Size * Size;
        public const int MatchWeightBeforeErasure = 1;
        public const int MatchWeightPerTierWhileErasing =
            DiceBehaviorConstants.JumboSinkingMatchWeightPerTier;
        /// <summary>
        /// Sink progress at which Top footprint occupancy is released (Bottom-only stage).
        /// </summary>
        public const float SinkTopOccupancyThreshold = 0.5f;

        public static void AppendCells(int anchorX, int anchorY, int[] xs, int[] ys, out int count)
        {
            count = 0;
            if (xs == null || ys == null || xs.Length < CellCount || ys.Length < CellCount)
            {
                return;
            }

            for (var dx = 0; dx < Size; dx++)
            {
                for (var dy = 0; dy < Size; dy++)
                {
                    xs[count] = anchorX + dx;
                    ys[count] = anchorY + dy;
                    count++;
                }
            }
        }

        public static bool Contains(int anchorX, int anchorY, int cellX, int cellY)
        {
            return cellX >= anchorX
                && cellX < anchorX + Size
                && cellY >= anchorY
                && cellY < anchorY + Size;
        }
    }
}
