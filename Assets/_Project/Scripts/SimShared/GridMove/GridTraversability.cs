namespace DiceGame.SimShared.GridMove
{
    using DiceGame.Core;
    using DiceGame.SimShared.Motion;

    /// <summary>
    /// Copied from production <c>GridTraversability</c>.
    /// </summary>
    public static class GridTraversability
    {
        public static int ToTierRank(DiceStackTier tier)
        {
            return tier == DiceStackTier.Top ? (int)CellOccupancyTier.Top : (int)CellOccupancyTier.Bottom;
        }

        public static bool CanTraverseCell(
            IGridRollOccupancy query,
            DiceStackTier fromTier,
            int cellX,
            int cellY,
            out string rejectReason)
        {
            rejectReason = null;
            var fromRank = ToTierRank(fromTier);

            if (!query.TryGetOccupancyTier(cellX, cellY, out var occupancy))
            {
                rejectReason = $"cell=({cellX},{cellY}) invalid-or-blocked";
                return false;
            }

            if ((int)occupancy >= fromRank)
            {
                rejectReason =
                    $"cell=({cellX},{cellY}) occupancy={occupancy} not-below fromTier={fromTier} rank={fromRank}";
                return false;
            }

            return true;
        }

        public static bool TryEvaluateRollPath(
            IGridRollOccupancy query,
            DiceStackTier fromTier,
            int fromX,
            int fromY,
            Direction direction,
            int distance,
            bool allowUpwardTier,
            DiceKind moverKind,
            out DiceStackTier landingTier,
            out GhostLandingMode ghostLanding,
            out DiceState ghostFrom,
            out DiceState ghostTo,
            out string rejectReason)
        {
            landingTier = default;
            ghostLanding = GhostLandingMode.None;
            ghostFrom = default;
            ghostTo = default;
            rejectReason = null;

            if (distance < 1)
            {
                rejectReason = "distance-too-small";
                return false;
            }

            direction.GetGridDelta(out var dx, out var dy);

            for (var step = 1; step < distance; step++)
            {
                var pathX = fromX + dx * step;
                var pathY = fromY + dy * step;
                var prevX = fromX + dx * (step - 1);
                var prevY = fromY + dy * (step - 1);
                if (query.BlocksRollBetween(prevX, prevY, pathX, pathY))
                {
                    rejectReason = $"traverse step={step}/{distance} blocked-by-partition cell=({pathX},{pathY})";
                    return false;
                }

                if (!CanTraverseCell(query, fromTier, pathX, pathY, out rejectReason))
                {
                    rejectReason = $"traverse step={step}/{distance} {rejectReason}";
                    return false;
                }
            }

            var landingX = fromX + dx * distance;
            var landingY = fromY + dy * distance;
            var prevLandX = fromX + dx * (distance - 1);
            var prevLandY = fromY + dy * (distance - 1);
            if (query.BlocksRollBetween(prevLandX, prevLandY, landingX, landingY))
            {
                rejectReason = $"land step={distance}/{distance} blocked-by-partition cell=({landingX},{landingY})";
                return false;
            }

            if (!CanLandAt(
                    query,
                    fromTier,
                    fromX,
                    fromY,
                    landingX,
                    landingY,
                    moverKind,
                    allowUpwardTier,
                    out landingTier,
                    out ghostLanding,
                    out ghostFrom,
                    out ghostTo,
                    out rejectReason))
            {
                rejectReason = $"land step={distance}/{distance} {rejectReason}";
                return false;
            }

            return true;
        }

        public static bool CanLandAt(
            IGridRollOccupancy query,
            DiceStackTier fromTier,
            int fromX,
            int fromY,
            int cellX,
            int cellY,
            DiceKind moverKind,
            bool allowUpwardTier,
            out DiceStackTier landingTier,
            out GhostLandingMode ghostLanding,
            out DiceState ghostFrom,
            out DiceState ghostTo,
            out string rejectReason)
        {
            landingTier = default;
            ghostLanding = GhostLandingMode.None;
            ghostFrom = default;
            ghostTo = default;
            rejectReason = null;
            var fromRank = ToTierRank(fromTier);

            if (!query.TryResolveLandingTier(
                    fromTier,
                    fromX,
                    fromY,
                    cellX,
                    cellY,
                    moverKind,
                    out landingTier,
                    out ghostLanding,
                    out ghostFrom,
                    out ghostTo))
            {
                rejectReason = $"cell=({cellX},{cellY}) no-valid-landing fromTier={fromTier}";
                return false;
            }

            var landingRank = ToTierRank(landingTier);
            if (landingRank > fromRank)
            {
                if (!allowUpwardTier)
                {
                    rejectReason =
                        $"cell=({cellX},{cellY}) landingTier={landingTier} rank={landingRank} above fromTier={fromTier} rank={fromRank}";
                    return false;
                }

                return true;
            }

            if (ghostLanding != GhostLandingMode.None)
            {
                return true;
            }

            if (landingRank == fromRank
                && fromTier == DiceStackTier.Top
                && query.CanOverwriteTopAt(cellX, cellY))
            {
                return true;
            }

            if (!CanTraverseCell(query, fromTier, cellX, cellY, out rejectReason))
            {
                rejectReason = $"cell=({cellX},{cellY}) land-occupancy {rejectReason}";
                return false;
            }

            return true;
        }

        public static DiceGridMoveKind ResolveMoveKind(DiceStackTier fromTier, DiceStackTier toTier)
        {
            return DiceGridMovePlanner.ResolveMoveKind(fromTier, toTier);
        }
    }
}
