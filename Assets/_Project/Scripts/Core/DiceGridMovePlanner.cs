using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Core
{
    public static class DiceGridMovePlanner
    {
        public static bool TryBuildJumpPlan(
            DiceState fromState,
            Direction direction,
            int distance,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (distance < 1 || distance > RollResolver.MaxParallelRollDistance) {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (fromState.Tier == DiceStackTier.Bottom && hasTopOnSameCell) {
                rejectReason = "has-top-on-start-cell";
                return false;
            }

            var landingCell = fromState.GridPos + direction.ToGridDelta() * distance;
            for (var step = 1; step <= distance; step++) {
                var pathCell = fromState.GridPos + direction.ToGridDelta() * step;
                if (!CanPassJumpRollCell(placement, fromState.Tier, pathCell, out var cellReject)) {
                    rejectReason = $"step={step}/{distance} target={FormatGrid(pathCell)} {cellReject}";
                    return false;
                }
            }

            if (!TryResolveLandingTier(fromState.Tier, landingCell, placement, out var landingTier, out var tierReject)) {
                rejectReason = $"landing={FormatGrid(landingCell)} {tierReject}";
                return false;
            }

            if (!TryBuildRolledState(fromState, landingCell, landingTier, direction, distance, out var toState)) {
                rejectReason = $"landing={FormatGrid(landingCell)} invalid-orientation";
                return false;
            }

            plan = new DiceGridMovePlan {
                From = fromState,
                To = toState,
                Kind = ResolveMoveKind(fromState.Tier, landingTier),
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
                useJumpPathRules: false,
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

        static bool TryResolveLandingTier(
            DiceStackTier fromTier,
            Vector2Int landingCell,
            IDicePlacement placement,
            out DiceStackTier landingTier,
            out string rejectReason) {
            landingTier = default;
            rejectReason = null;

            if (fromTier == DiceStackTier.Bottom) {
                if (placement.CanPlaceBottomDiceAt(landingCell)) {
                    landingTier = DiceStackTier.Bottom;
                    return true;
                }

                if (placement.CanPlaceTopDiceAt(landingCell)) {
                    landingTier = DiceStackTier.Top;
                    return true;
                }

                rejectReason = "bottom-start invalid-landing";
                return false;
            }

            if (placement.CanPlaceBottomDiceAt(landingCell)) {
                landingTier = DiceStackTier.Bottom;
                return true;
            }

            if (placement.CanPlaceTopDiceAt(landingCell)) {
                landingTier = DiceStackTier.Top;
                return true;
            }

            rejectReason = "top-start invalid-landing";
            return false;
        }

        public static bool CanPassJumpRollCell(
            IDicePlacement placement,
            DiceStackTier tier,
            Vector2Int targetPos,
            out string rejectReason) {
            rejectReason = null;

            if (tier == DiceStackTier.Bottom) {
                if (placement.CanPlaceBottomDiceAt(targetPos)) {
                    return true;
                }

                rejectReason = "bottom-path-blocked not-empty-floor";
                return false;
            }

            if (placement.CanPlaceBottomDiceAt(targetPos) || placement.CanPlaceTopDiceAt(targetPos)) {
                return true;
            }

            rejectReason = "top-path-blocked has-top-dice";
            return false;
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
