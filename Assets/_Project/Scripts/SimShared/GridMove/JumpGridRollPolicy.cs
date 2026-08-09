namespace DiceGame.SimShared.GridMove
{
    using DiceGame.Core;
    using DiceGame.SimShared.Motion;

    /// <summary>
    /// Copied from production <c>GridMovePlanBuilder.TryBuild</c>.
    /// </summary>
    public static class GridMovePlanBuilder
    {
        public static bool TryBuild(
            IGridRollOccupancy occupancy,
            DiceState fromState,
            Direction direction,
            int distance,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason)
        {
            plan = default;
            rejectReason = null;

            var hasTopOnSameCell = occupancy.HasSolidTopAt(fromState.GridX, fromState.GridY);
            if (!DiceGridPassability.TryEvaluate(
                    occupancy,
                    fromState,
                    direction,
                    distance,
                    hasTopOnSameCell,
                    context,
                    out var landingTier,
                    out var moveKind,
                    out var ghostLanding,
                    out var ghostFrom,
                    out var ghostTo,
                    out rejectReason))
            {
                return false;
            }

            if (!DiceGridMovePlanner.TryBuildPlan(
                    fromState,
                    direction,
                    distance,
                    landingTier,
                    moveKind,
                    out plan,
                    out rejectReason))
            {
                return false;
            }

            if (ghostLanding != GhostLandingMode.None)
            {
                plan.GhostLanding = ghostLanding;
                plan.GhostFrom = ghostFrom;
                plan.GhostTo = ghostTo;
                if (ghostLanding == GhostLandingMode.InCellPromoteGhost)
                {
                    plan.To = new DiceState(
                        plan.To.GridX,
                        plan.To.GridY,
                        plan.To.Orientation,
                        DiceStackTier.Bottom,
                        plan.To.Kind);
                    plan.Kind = GridTraversability.ResolveMoveKind(fromState.Tier, DiceStackTier.Bottom);
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Copied from production <c>JumpGridRollPolicy.TryCreateCoupledTransition</c> (plan-only Domain).
    /// </summary>
    public static class JumpGridRollPolicy
    {
        public static bool TryBuildPlan(
            IGridRollOccupancy occupancy,
            DiceState fromState,
            Direction direction,
            int distance,
            int maxJumpGridMoveDistance,
            bool allowsRoll,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason)
        {
            plan = default;
            rejectReason = null;

            if (!allowsRoll)
            {
                rejectReason = "surface-disallows-roll";
                return false;
            }

            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance)
            {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (distance > maxJumpGridMoveDistance)
            {
                rejectReason = "beyond-kind-max-jump-grid-distance";
                return false;
            }

            return GridMovePlanBuilder.TryBuild(
                occupancy,
                fromState,
                direction,
                distance,
                context,
                out plan,
                out rejectReason);
        }

        /// <summary>
        /// Production loop: try maxDistance down to 1 along facing direction.
        /// </summary>
        public static bool TryBuildBestPlan(
            IGridRollOccupancy occupancy,
            DiceState fromState,
            Direction direction,
            int maxDistance,
            int maxJumpGridMoveDistance,
            bool allowsRoll,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason)
        {
            plan = default;
            rejectReason = "no-distance";
            var capped = maxDistance;
            if (capped > DiceGridRollLimits.MaxParallelRollDistance)
            {
                capped = DiceGridRollLimits.MaxParallelRollDistance;
            }

            if (maxJumpGridMoveDistance > 0 && capped > maxJumpGridMoveDistance)
            {
                capped = maxJumpGridMoveDistance;
            }

            for (var distance = capped; distance >= 1; distance--)
            {
                if (TryBuildPlan(
                        occupancy,
                        fromState,
                        direction,
                        distance,
                        maxJumpGridMoveDistance > 0 ? maxJumpGridMoveDistance : capped,
                        allowsRoll,
                        context,
                        out plan,
                        out rejectReason))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
