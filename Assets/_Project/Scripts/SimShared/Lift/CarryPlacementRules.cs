namespace DiceGame.SimShared.Lift
{
    /// <summary>
    /// Production <c>CarryPlacementPassability.TryResolveTarget</c> with place queries.
    /// </summary>
    public static class CarryPlacementRules
    {
        public delegate bool CellQuery(int x, int y);

        public static bool TryResolveTarget(
            int targetX,
            int targetY,
            CellQuery canPlaceBottom,
            CellQuery canPlaceTop,
            CellQuery canAcceptTop,
            out int targetTier)
        {
            targetTier = 0;
            if (canPlaceBottom != null && canPlaceBottom(targetX, targetY))
            {
                targetTier = 0;
                return true;
            }

            if (canPlaceTop != null && canPlaceTop(targetX, targetY))
            {
                targetTier = 1;
                return true;
            }

            if (canAcceptTop != null && canAcceptTop(targetX, targetY))
            {
                targetTier = 1;
                return true;
            }

            return false;
        }
    }
}
