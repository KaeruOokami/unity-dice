namespace DiceGame.SimShared.Ghost
{
    /// <summary>
    /// Pure ghost swap predicates (production <c>GhostPlacementRules</c> cell-swap subset).
    /// </summary>
    public static class GhostSwapRules
    {
        public const int ModeNone = 0;
        public const int ModeCellSwap = 1;
        public const int ModeInCellPromote = 2;

        public struct Occupant
        {
            public bool Valid;
            public bool AllowsDiceSwapThrough;
            public bool IsPlayerPassThroughKind;
            public bool IsBusy;
            public bool IsErasing;
            public bool IsCarried;
            public int Tier;
            public int CellX;
            public int CellY;
        }

        public static bool TryResolveSameTierCellSwap(
            int moverTier,
            bool moverIsPassThroughKind,
            int moverFromX,
            int moverFromY,
            in Occupant ghost,
            out int moverToX,
            out int moverToY,
            out int moverToTier,
            out int ghostToX,
            out int ghostToY,
            out int ghostToTier)
        {
            moverToX = moverFromX;
            moverToY = moverFromY;
            moverToTier = moverTier;
            ghostToX = ghost.CellX;
            ghostToY = ghost.CellY;
            ghostToTier = ghost.Tier;

            if (!ghost.Valid
                || moverIsPassThroughKind
                || !ghost.AllowsDiceSwapThrough
                || ghost.Tier != moverTier
                || ghost.IsBusy
                || ghost.IsErasing
                || ghost.IsCarried)
            {
                return false;
            }

            moverToX = ghost.CellX;
            moverToY = ghost.CellY;
            moverToTier = moverTier;
            ghostToX = moverFromX;
            ghostToY = moverFromY;
            ghostToTier = ghost.Tier;
            return true;
        }

        public static bool TryResolveInCellPromote(
            bool moverIsPassThroughKind,
            int cellX,
            int cellY,
            in Occupant ghostBottom,
            out int moverToTier,
            out int ghostToTier)
        {
            moverToTier = 0;
            ghostToTier = 1;
            if (!ghostBottom.Valid
                || moverIsPassThroughKind
                || !ghostBottom.AllowsDiceSwapThrough
                || ghostBottom.Tier != 0
                || ghostBottom.IsBusy
                || ghostBottom.IsErasing
                || ghostBottom.IsCarried
                || ghostBottom.CellX != cellX
                || ghostBottom.CellY != cellY)
            {
                return false;
            }

            return true;
        }

        public static bool TryResolveAscentGhostSwap(
            bool moverIsPassThroughKind,
            int moverFromX,
            int moverFromY,
            in Occupant topGhost,
            out int moverToX,
            out int moverToY,
            out int ghostToX,
            out int ghostToY)
        {
            moverToX = topGhost.CellX;
            moverToY = topGhost.CellY;
            ghostToX = moverFromX;
            ghostToY = moverFromY;

            if (!topGhost.Valid
                || moverIsPassThroughKind
                || !topGhost.AllowsDiceSwapThrough
                || topGhost.Tier != 1
                || topGhost.IsBusy
                || topGhost.IsErasing
                || topGhost.IsCarried)
            {
                return false;
            }

            return true;
        }
    }
}
