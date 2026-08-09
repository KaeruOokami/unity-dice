namespace DiceGame.SimShared.Push
{
    /// <summary>
    /// One-cell push destination (Wood roll / Normal slide). Ice multi-cell deferred.
    /// Tier: Bottom=0, Top=1.
    /// </summary>
    public static class OneCellPushPlanner
    {
        public delegate bool CellQuery(int x, int y);

        public static bool TryPlan(
            int fromX,
            int fromY,
            int fromTier,
            int dirX,
            int dirY,
            int boardWidth,
            int boardHeight,
            CellQuery canPlaceBottomAt,
            CellQuery canPlaceTopAt,
            CellQuery hasSolidBottomAt,
            out int destX,
            out int destY,
            out int landingTier,
            out bool demoteUnsupportedTopAtFrom)
        {
            destX = fromX + dirX;
            destY = fromY + dirY;
            landingTier = fromTier;
            demoteUnsupportedTopAtFrom = false;

            if (!IsInside(boardWidth, boardHeight, destX, destY))
            {
                return false;
            }

            if (fromTier == 0)
            {
                if (canPlaceBottomAt == null || !canPlaceBottomAt(destX, destY))
                {
                    return false;
                }

                demoteUnsupportedTopAtFrom = true;
                landingTier = 0;
                return true;
            }

            if (canPlaceTopAt != null && canPlaceTopAt(destX, destY))
            {
                landingTier = 1;
                return true;
            }

            if (canPlaceBottomAt != null && canPlaceBottomAt(destX, destY))
            {
                // Top pushed onto unsupported cell demotes to Bottom.
                landingTier = 0;
                return true;
            }

            // Explicit unused check keeps hasSolidBottomAt in the API for future Ghost/swap.
            _ = hasSolidBottomAt;
            return false;
        }

        static bool IsInside(int width, int height, int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }
    }
}
