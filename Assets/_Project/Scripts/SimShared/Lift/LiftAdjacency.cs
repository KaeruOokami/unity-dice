namespace DiceGame.SimShared.Lift
{
    /// <summary>
    /// Copied from production <c>DiceStackAdjacency.IsAdjacentForLift</c> / facing neighbor lift.
    /// </summary>
    public static class LiftAdjacency
    {
        public static bool IsAdjacentForLift(int fromX, int fromY, int toX, int toY)
        {
            var dx = toX - fromX;
            var dy = toY - fromY;
            if (dx == 0 && dy == 0)
            {
                return false;
            }

            return (dx == 0 || dy == 0) && System.Math.Abs(dx) + System.Math.Abs(dy) == 1;
        }

        public static void FacingNeighbor(int cellX, int cellY, int facingX, int facingY, out int nx, out int ny)
        {
            nx = cellX + facingX;
            ny = cellY + facingY;
        }
    }
}
