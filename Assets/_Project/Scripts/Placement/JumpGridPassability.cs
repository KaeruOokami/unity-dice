using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class JumpGridPassability
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

            if (!context.IsJumping) {
                rejectReason = "not-jumping";
                return false;
            }

            if (!context.AllowJumpGridMove) {
                rejectReason = "jump-grid-move-not-allowed";
                return false;
            }

            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance) {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (fromState.Tier == DiceStackTier.Bottom && hasTopOnSameCell) {
                rejectReason = "has-top-on-start-cell";
                return false;
            }

            if (!GridTraversability.TryEvaluateRollPath(
                occupancyQuery,
                fromState.Tier,
                fromState.GridPos,
                direction,
                distance,
                allowUpwardTier: distance == 1,
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
            if (moveKind != DiceGridMoveKind.Parallel
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
