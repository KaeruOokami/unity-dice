namespace DiceGame.SimShared.Magnet
{
    /// <summary>
    /// Same-tier adjacent magnet blocker scan (production <c>IronAdjacencyBlock</c>).
    /// </summary>
    public static class MagnetAdjacencyBlock
    {
        static readonly int[] NeighborX = { 0, 1, 0, -1 };
        static readonly int[] NeighborY = { 1, 0, -1, 0 };

        public delegate bool TryGetNeighbor(
            int x,
            int y,
            int tier,
            out bool blocksAdjacentMagnet,
            out bool isErasing);

        public static bool HasAdjacentMagnetBlocker(
            int cellX,
            int cellY,
            int tier,
            TryGetNeighbor tryGetNeighbor)
        {
            if (tryGetNeighbor == null)
            {
                return false;
            }

            for (var i = 0; i < 4; i++)
            {
                var x = cellX + NeighborX[i];
                var y = cellY + NeighborY[i];
                if (tryGetNeighbor(x, y, tier, out var blocks, out var erasing)
                    && blocks
                    && !erasing)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
