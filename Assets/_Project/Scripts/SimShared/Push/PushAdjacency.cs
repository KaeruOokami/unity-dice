namespace DiceGame.SimShared.Push
{
    /// <summary>
    /// Pure push adjacency matching <c>DiceStackAdjacency.IsAdjacentForPush</c>.
    /// Tier norms: Bottom=0, Top=1.
    /// </summary>
    public static class PushAdjacency
    {
        public static bool IsAdjacentForPush(
            int playerCellX,
            int playerCellY,
            int playerTier,
            bool isOnFloor,
            int diceCellX,
            int diceCellY,
            int diceTier)
        {
            if (isOnFloor)
            {
                return diceTier == 0 && IsHorizontalGridAdjacent(
                    playerCellX, playerCellY, 0,
                    diceCellX, diceCellY, diceTier);
            }

            if (playerTier == 0)
            {
                return diceTier == 1 && IsHorizontalNeighbor(playerCellX, playerCellY, diceCellX, diceCellY);
            }

            return diceTier == 1 && IsHorizontalGridAdjacent(
                playerCellX, playerCellY, playerTier,
                diceCellX, diceCellY, diceTier);
        }

        static bool IsHorizontalGridAdjacent(
            int ax, int ay, int aTier,
            int bx, int by, int bTier)
        {
            if (aTier != bTier)
            {
                return false;
            }

            return IsHorizontalNeighbor(ax, ay, bx, by);
        }

        static bool IsHorizontalNeighbor(int ax, int ay, int bx, int by)
        {
            var dx = ax > bx ? ax - bx : bx - ax;
            var dy = ay > by ? ay - by : by - ay;
            return dx + dy == 1;
        }
    }
}
