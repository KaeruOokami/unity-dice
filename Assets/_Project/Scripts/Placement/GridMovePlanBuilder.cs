using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public sealed class GridMovePlanBuilder
    {
        readonly DiceRegistry registry;
        readonly CellOccupancyQuery occupancyQuery;

        public GridMovePlanBuilder(DiceRegistry registry, CellOccupancyQuery occupancyQuery) {
            this.registry = registry;
            this.occupancyQuery = occupancyQuery;
        }

        public bool TryBuild(
            DiceState fromState,
            Direction direction,
            int distance,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            var hasTopOnSameCell = registry.HasTopAt(fromState.GridPos);
            if (!DiceGridPassability.TryEvaluate(
                occupancyQuery,
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
                out rejectReason)) {
                return false;
            }

            if (!DiceGridMovePlanner.TryBuildPlan(
                fromState,
                direction,
                distance,
                landingTier,
                moveKind,
                out plan,
                out rejectReason)) {
                return false;
            }

            if (ghostLanding != GhostLandingMode.None) {
                plan.GhostLanding = ghostLanding;
                plan.GhostFrom = ghostFrom;
                plan.GhostTo = ghostTo;
                if (ghostLanding == GhostLandingMode.InCellPromoteGhost) {
                    plan.To = new DiceState(
                        plan.To.GridPos,
                        plan.To.Orientation,
                        DiceStackTier.Bottom,
                        plan.To.Kind);
                    plan.Kind = GridTraversability.ResolveMoveKind(fromState.Tier, DiceStackTier.Bottom);
                }
            }

            return true;
        }
    }
}
