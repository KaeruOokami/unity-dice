using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Core
{
    public static class DiceGridMovePlanner
    {
        public static bool TryBuildPlan(
            DiceState fromState,
            Direction direction,
            int distance,
            DiceStackTier landingTier,
            DiceGridMoveKind kind,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (distance < 1 || distance > RollResolver.MaxParallelRollDistance) {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            var expectedKind = ResolveMoveKind(fromState.Tier, landingTier);
            if (kind != expectedKind) {
                rejectReason = $"kind-mismatch expected={expectedKind} actual={kind}";
                return false;
            }

            var landingCell = fromState.GridPos + direction.ToGridDelta() * distance;
            if (!TryBuildRolledState(fromState, landingCell, landingTier, direction, distance, out var toState)) {
                rejectReason = $"landing={FormatGrid(landingCell)} invalid-orientation";
                return false;
            }

            plan = new DiceGridMovePlan {
                From = fromState,
                To = toState,
                Kind = kind,
                Direction = direction,
                Distance = distance
            };
            return true;
        }

        public static bool TryBuildGroundParallelPlan(
            DiceState fromState,
            Direction direction,
            int distance,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (!RollResolver.TryRollDistance(
                fromState,
                direction,
                placement,
                hasTopOnSameCell,
                distance,
                out var nextState,
                out rejectReason)) {
                return false;
            }

            plan = new DiceGridMovePlan {
                From = fromState,
                To = nextState,
                Kind = DiceGridMoveKind.Parallel,
                Direction = direction,
                Distance = distance
            };
            return true;
        }

        static DiceGridMoveKind ResolveMoveKind(DiceStackTier fromTier, DiceStackTier toTier) {
            if (fromTier == toTier) {
                return DiceGridMoveKind.Parallel;
            }

            return fromTier == DiceStackTier.Top
                ? DiceGridMoveKind.Demote
                : DiceGridMoveKind.Stack;
        }

        static bool TryBuildRolledState(
            DiceState fromState,
            Vector2Int landingCell,
            DiceStackTier landingTier,
            Direction direction,
            int distance,
            out DiceState toState) {
            toState = default;

            var orientation = fromState.Orientation;
            for (var step = 0; step < distance; step++) {
                orientation = orientation.Roll(direction);
                if (!orientation.IsValid()) {
                    return false;
                }
            }

            toState = new DiceState(landingCell, orientation, landingTier);
            return true;
        }

        static string FormatGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
