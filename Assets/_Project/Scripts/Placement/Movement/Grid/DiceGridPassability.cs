using DiceGame.Core;

namespace DiceGame.Placement
{
    /// <summary>
    /// Unified ground/jump grid-roll passability. Jump vs ground differences come from
    /// <see cref="PassabilityContext"/> (not separate classes).
    /// </summary>
    public static class DiceGridPassability
    {
        public static bool TryEvaluate(
            CellOccupancyQuery occupancyQuery,
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
            out string rejectReason) {
            landingTier = default;
            moveKind = default;
            ghostLanding = GhostLandingMode.None;
            ghostFrom = default;
            ghostTo = default;
            rejectReason = null;

            if (context.IsJumping) {
                if (!context.AllowJumpGridMove) {
                    rejectReason = "jump-grid-move-not-allowed";
                    return false;
                }
            }

            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance) {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (fromState.Tier == DiceStackTier.Bottom && hasTopOnSameCell) {
                rejectReason = "has-top-on-start-cell";
                return false;
            }

            var allowUpwardTier = context.IsJumping
                ? distance == 1
                : occupancyQuery.CanOverwriteTopAt(fromState.GridPos + direction.ToGridDelta() * distance);

            if (!GridTraversability.TryEvaluateRollPath(
                occupancyQuery,
                fromState.Tier,
                fromState.GridPos,
                direction,
                distance,
                allowUpwardTier,
                fromState.Kind,
                out landingTier,
                out ghostLanding,
                out ghostFrom,
                out ghostTo,
                out rejectReason)) {
                return false;
            }

            moveKind = ghostLanding == GhostLandingMode.InCellPromoteGhost
                ? GridTraversability.ResolveMoveKind(fromState.Tier, DiceStackTier.Bottom)
                : GridTraversability.ResolveMoveKind(fromState.Tier, landingTier);

            if (context.IsJumping
                && moveKind != DiceGridMoveKind.Parallel
                && distance == 1
                && !context.AllowJumpTierChange
                && ghostLanding == GhostLandingMode.None) {
                rejectReason = $"tier-change-not-allowed kind={moveKind}";
                return false;
            }

            return true;
        }
    }
}
