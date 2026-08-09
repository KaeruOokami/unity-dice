namespace DiceGame.SimShared.Board
{
    /// <summary>
    /// Versus partition boundary (production <c>VersusArenaLayout.CrossesPartition</c>).
    /// PartitionX &lt;= 0 disables the boundary.
    /// </summary>
    public static class PartitionRules
    {
        public static bool CrossesPartition(int partitionX, int fromX, int toX)
        {
            if (partitionX <= 0)
            {
                return false;
            }

            var fromPlayer1 = fromX < partitionX;
            var toPlayer1 = toX < partitionX;
            return fromPlayer1 != toPlayer1;
        }

        public static bool BlocksTraversal(
            int partitionX,
            bool ignoresPartitionBoundary,
            int fromX,
            int toX)
        {
            if (ignoresPartitionBoundary)
            {
                return false;
            }

            return CrossesPartition(partitionX, fromX, toX);
        }

        public static bool TryGetPartitionDismountCell(
            int partitionX,
            int fromX,
            int fromY,
            int toX,
            int toY,
            int dirX,
            int dirY,
            out int dismountX,
            out int dismountY)
        {
            dismountX = fromX;
            dismountY = fromY;
            if (partitionX <= 0 || !CrossesPartition(partitionX, fromX, toX))
            {
                return false;
            }

            var cellX = fromX;
            var cellY = fromY;
            var guard = 0;
            while ((cellX != toX || cellY != toY) && guard++ < 64)
            {
                var nextX = cellX + dirX;
                var nextY = cellY + dirY;
                if (CrossesPartition(partitionX, cellX, nextX))
                {
                    dismountX = cellX;
                    dismountY = cellY;
                    return true;
                }

                cellX = nextX;
                cellY = nextY;
            }

            return false;
        }
    }
}
