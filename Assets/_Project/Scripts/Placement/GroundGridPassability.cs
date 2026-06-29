using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class GroundGridPassability
    {
        public static bool TryEvaluate(
            CellOccupancyQuery occupancyQuery,
            DiceState fromState,
            Direction direction,
            int distance,
            bool hasTopOnSameCell,
            out DiceStackTier landingTier,
            out DiceGridMoveKind moveKind,
            out string rejectReason) {
            landingTier = default;
            moveKind = default;
            rejectReason = null;

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
                out landingTier,
                out rejectReason)) {
                return false;
            }

            moveKind = GridTraversability.ResolveMoveKind(fromState.Tier, landingTier);
            return true;
        }
    }
}
