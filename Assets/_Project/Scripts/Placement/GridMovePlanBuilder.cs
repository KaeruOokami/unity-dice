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
            bool passable;
            DiceStackTier landingTier;
            DiceGridMoveKind moveKind;

            if (context.IsJumping) {
                passable = JumpGridPassability.TryEvaluate(
                    occupancyQuery,
                    fromState,
                    direction,
                    distance,
                    hasTopOnSameCell,
                    context,
                    out landingTier,
                    out moveKind,
                    out rejectReason);
            } else {
                passable = GroundGridPassability.TryEvaluate(
                    occupancyQuery,
                    fromState,
                    direction,
                    distance,
                    hasTopOnSameCell,
                    out landingTier,
                    out moveKind,
                    out rejectReason);
            }

            if (!passable) {
                return false;
            }

            return DiceGridMovePlanner.TryBuildPlan(
                fromState,
                direction,
                distance,
                landingTier,
                moveKind,
                out plan,
                out rejectReason);
        }
    }
}
