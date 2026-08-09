namespace DiceGame.SimShared.GridMove
{
    using DiceGame.Core;
    using DiceGame.SimShared.Motion;

    /// <summary>
    /// Copied from production <c>DiceGridPassability</c>.
    /// </summary>
    public static class DiceGridPassability
    {
        public static bool TryEvaluate(
            IGridRollOccupancy occupancyQuery,
            DiceState fromState,
            Direction direction,
            int distance,
            bool hasTopOnSameCell,
            PassabilityContext context,
            out DiceStackTier landingTier,
            out DiceGridMoveKind moveKind,
            out GhostLandingMode ghostLanding,
            out DiceState ghostFrom,
            out DiceState ghostTo,
            out string rejectReason)
        {
            landingTier = default;
            moveKind = default;
            ghostLanding = GhostLandingMode.None;
            ghostFrom = default;
            ghostTo = default;
            rejectReason = null;

            if (context.IsJumping)
            {
                if (!context.AllowJumpGridMove)
                {
                    rejectReason = "jump-grid-move-not-allowed";
                    return false;
                }
            }

            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance)
            {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (fromState.Tier == DiceStackTier.Bottom && hasTopOnSameCell)
            {
                rejectReason = "has-top-on-start-cell";
                return false;
            }

            direction.GetGridDelta(out var dx, out var dy);
            var destX = fromState.GridX + dx * distance;
            var destY = fromState.GridY + dy * distance;
            var allowUpwardTier = context.IsJumping
                ? distance == 1
                : occupancyQuery.CanOverwriteTopAt(destX, destY);

            if (!GridTraversability.TryEvaluateRollPath(
                    occupancyQuery,
                    fromState.Tier,
                    fromState.GridX,
                    fromState.GridY,
                    direction,
                    distance,
                    allowUpwardTier,
                    fromState.Kind,
                    out landingTier,
                    out ghostLanding,
                    out ghostFrom,
                    out ghostTo,
                    out rejectReason))
            {
                return false;
            }

            moveKind = ghostLanding == GhostLandingMode.InCellPromoteGhost
                ? GridTraversability.ResolveMoveKind(fromState.Tier, DiceStackTier.Bottom)
                : GridTraversability.ResolveMoveKind(fromState.Tier, landingTier);

            if (context.IsJumping
                && moveKind != DiceGridMoveKind.Parallel
                && distance == 1
                && !context.AllowJumpTierChange
                && ghostLanding == GhostLandingMode.None)
            {
                rejectReason = $"tier-change-not-allowed kind={moveKind}";
                return false;
            }

            return true;
        }
    }
}
