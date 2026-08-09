namespace DiceGame.SimShared.Slide
{
    using DiceGame.SimShared.Board;
    using DiceGame.SimShared.Ghost;
    using DiceGame.SimShared.Push;

    /// <summary>
    /// Production <c>IceSlidePassability.TryBuildUntilBlocked</c> with Ghost swap + partition.
    /// </summary>
    public static class IceSlideUntilBlockedPlanner
    {
        public const int MaxSteps = 32;

        public struct PlanResult
        {
            public int DestX;
            public int DestY;
            public int LandingTier;
            public int StepCount;
            public bool DemoteUnsupportedTopAtFrom;
            public bool HasGhostSwap;
            public int GhostMode;
            public int GhostFromX;
            public int GhostFromY;
            public int GhostFromTier;
            public int GhostToX;
            public int GhostToY;
            public int GhostToTier;
            public bool HasPartitionDismount;
            public int DismountX;
            public int DismountY;
        }

        public delegate bool TryGetOccupant(int x, int y, int tier, out GhostSwapRules.Occupant occupant);

        public static bool TryPlan(
            int fromX,
            int fromY,
            int fromTier,
            int dirX,
            int dirY,
            int boardWidth,
            int boardHeight,
            int partitionX,
            bool ignoresPartitionBoundary,
            bool moverIsPassThroughKind,
            OneCellPushPlanner.CellQuery canPlaceSolidBottomAt,
            OneCellPushPlanner.CellQuery canPlaceSolidTopAt,
            OneCellPushPlanner.CellQuery hasSolidBottomAt,
            TryGetOccupant tryGetOccupant,
            out PlanResult result)
        {
            result = default;
            result.DestX = fromX;
            result.DestY = fromY;
            result.LandingTier = fromTier;

            if ((dirX == 0 && dirY == 0)
                || (dirX != 0 && dirY != 0)
                || (dirX != 0 && dirX != 1 && dirX != -1)
                || (dirY != 0 && dirY != 1 && dirY != -1))
            {
                return false;
            }

            var curX = fromX;
            var curY = fromY;
            var curTier = fromTier;
            var firstDemote = false;
            var stepCount = 0;

            while (stepCount < MaxSteps)
            {
                var nextX = curX + dirX;
                var nextY = curY + dirY;
                if (PartitionRules.BlocksTraversal(partitionX, ignoresPartitionBoundary, curX, nextX))
                {
                    break;
                }

                if (tryGetOccupant != null
                    && tryGetOccupant(nextX, nextY, curTier, out var sameTierGhost)
                    && GhostSwapRules.TryResolveSameTierCellSwap(
                        curTier,
                        moverIsPassThroughKind,
                        curX,
                        curY,
                        in sameTierGhost,
                        out var moverToX,
                        out var moverToY,
                        out var moverToTier,
                        out var ghostToX,
                        out var ghostToY,
                        out var ghostToTier))
                {
                    if (stepCount == 0)
                    {
                        firstDemote = curTier == 0;
                    }

                    stepCount++;
                    result.HasGhostSwap = true;
                    result.GhostMode = GhostSwapRules.ModeCellSwap;
                    result.GhostFromX = sameTierGhost.CellX;
                    result.GhostFromY = sameTierGhost.CellY;
                    result.GhostFromTier = sameTierGhost.Tier;
                    result.GhostToX = ghostToX;
                    result.GhostToY = ghostToY;
                    result.GhostToTier = ghostToTier;
                    curX = moverToX;
                    curY = moverToY;
                    curTier = moverToTier;
                    break;
                }

                if (!OneCellPushPlanner.TryPlan(
                        curX,
                        curY,
                        curTier,
                        dirX,
                        dirY,
                        boardWidth,
                        boardHeight,
                        canPlaceSolidBottomAt,
                        canPlaceSolidTopAt,
                        hasSolidBottomAt,
                        out nextX,
                        out nextY,
                        out var nextTier,
                        out var demoteTop))
                {
                    break;
                }

                if (stepCount == 0)
                {
                    firstDemote = demoteTop;
                }

                // Top → Bottom onto ghost Bottom: in-cell promote, stop.
                if (curTier == 1
                    && nextTier == 0
                    && tryGetOccupant != null
                    && tryGetOccupant(nextX, nextY, 0, out var ghostBottom)
                    && GhostSwapRules.TryResolveInCellPromote(
                        moverIsPassThroughKind,
                        nextX,
                        nextY,
                        in ghostBottom,
                        out _,
                        out var ghostPromoteTier))
                {
                    stepCount++;
                    result.HasGhostSwap = true;
                    result.GhostMode = GhostSwapRules.ModeInCellPromote;
                    result.GhostFromX = nextX;
                    result.GhostFromY = nextY;
                    result.GhostFromTier = 0;
                    result.GhostToX = nextX;
                    result.GhostToY = nextY;
                    result.GhostToTier = ghostPromoteTier;
                    curX = nextX;
                    curY = nextY;
                    curTier = 0;
                    break;
                }

                // Bottom → Top landing with Top ghost: ascent swap, stop.
                if (curTier == 0
                    && nextTier == 1
                    && tryGetOccupant != null
                    && tryGetOccupant(nextX, nextY, 1, out var topGhost)
                    && GhostSwapRules.TryResolveAscentGhostSwap(
                        moverIsPassThroughKind,
                        curX,
                        curY,
                        in topGhost,
                        out var ascMoverX,
                        out var ascMoverY,
                        out var ascGhostX,
                        out var ascGhostY))
                {
                    stepCount++;
                    result.HasGhostSwap = true;
                    result.GhostMode = GhostSwapRules.ModeCellSwap;
                    result.GhostFromX = topGhost.CellX;
                    result.GhostFromY = topGhost.CellY;
                    result.GhostFromTier = 1;
                    result.GhostToX = ascGhostX;
                    result.GhostToY = ascGhostY;
                    result.GhostToTier = 0;
                    curX = ascMoverX;
                    curY = ascMoverY;
                    curTier = 1;
                    break;
                }

                stepCount++;
                var fell = curTier == 1 && nextTier == 0;
                curX = nextX;
                curY = nextY;
                curTier = nextTier;
                if (fell)
                {
                    break;
                }
            }

            if (stepCount == 0)
            {
                return false;
            }

            result.DestX = curX;
            result.DestY = curY;
            result.LandingTier = curTier;
            result.StepCount = stepCount;
            result.DemoteUnsupportedTopAtFrom = firstDemote;
            if (PartitionRules.TryGetPartitionDismountCell(
                    partitionX,
                    fromX,
                    fromY,
                    curX,
                    curY,
                    dirX,
                    dirY,
                    out var dx,
                    out var dy))
            {
                result.HasPartitionDismount = true;
                result.DismountX = dx;
                result.DismountY = dy;
            }

            return true;
        }

        /// <summary>Legacy overload without Ghost/partition (solid place only).</summary>
        public static bool TryPlan(
            int fromX,
            int fromY,
            int fromTier,
            int dirX,
            int dirY,
            int boardWidth,
            int boardHeight,
            OneCellPushPlanner.CellQuery canPlaceBottomAt,
            OneCellPushPlanner.CellQuery canPlaceTopAt,
            OneCellPushPlanner.CellQuery hasSolidBottomAt,
            out int destX,
            out int destY,
            out int landingTier,
            out int stepCount,
            out bool demoteUnsupportedTopAtFrom)
        {
            var ok = TryPlan(
                fromX,
                fromY,
                fromTier,
                dirX,
                dirY,
                boardWidth,
                boardHeight,
                partitionX: 0,
                ignoresPartitionBoundary: true,
                moverIsPassThroughKind: false,
                canPlaceBottomAt,
                canPlaceTopAt,
                hasSolidBottomAt,
                tryGetOccupant: null,
                out var plan);
            destX = plan.DestX;
            destY = plan.DestY;
            landingTier = plan.LandingTier;
            stepCount = plan.StepCount;
            demoteUnsupportedTopAtFrom = plan.DemoteUnsupportedTopAtFrom;
            return ok;
        }

        public static int ResolveMotionTicks(int baseTicksPerCell, int stepCount)
        {
            var steps = stepCount > 0 ? stepCount : 1;
            var perCell = baseTicksPerCell > 0 ? baseTicksPerCell : 1;
            return perCell * steps;
        }
    }
}
