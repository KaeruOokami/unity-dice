using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Core
{
    public static class RollResolver
    {
        public const int MaxParallelRollDistance = 2;

        public static bool TryRoll(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            out DiceState nextState) {
            return TryRollDistance(state, direction, placement, hasTopOnSameCell, 1, out nextState, out _);
        }

        public static bool TryRollDistance(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            int distance,
            out DiceState nextState) {
            return TryRollDistance(
                state,
                direction,
                placement,
                hasTopOnSameCell,
                distance,
                out nextState,
                out _);
        }

        public static bool TryRollDistance(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            int distance,
            out DiceState nextState,
            out string rejectReason) {
            return TryRollDistance(
                state,
                direction,
                placement,
                hasTopOnSameCell,
                distance,
                useJumpPathRules: false,
                out nextState,
                out rejectReason);
        }

        public static bool TryRollDistance(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            int distance,
            bool useJumpPathRules,
            out DiceState nextState,
            out string rejectReason) {
            nextState = default;
            rejectReason = null;

            if (distance < 1 || distance > MaxParallelRollDistance) {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (useJumpPathRules) {
                return TryRollDistanceJumpPath(
                    state,
                    direction,
                    placement,
                    hasTopOnSameCell,
                    distance,
                    out nextState,
                    out rejectReason);
            }

            return TryRollDistanceGroundPath(
                state,
                direction,
                placement,
                hasTopOnSameCell,
                distance,
                out nextState,
                out rejectReason);
        }

        static bool TryRollDistanceJumpPath(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            int distance,
            out DiceState nextState,
            out string rejectReason) {
            nextState = default;
            rejectReason = null;

            if (state.Tier == DiceStackTier.Bottom && hasTopOnSameCell) {
                rejectReason = "has-top-on-start-cell";
                return false;
            }

            var rollingState = state;
            for (var step = 0; step < distance; step++) {
                var targetPos = rollingState.GridPos + direction.ToGridDelta();
                if (!CanPassJumpRollCell(placement, state.Tier, targetPos, out var cellReject)) {
                    rejectReason = $"step={step + 1}/{distance} target={FormatGrid(targetPos)} {cellReject}";
                    return false;
                }

                if (!TryBuildRolledState(rollingState, targetPos, state.Tier, direction, out rollingState)) {
                    rejectReason = $"step={step + 1}/{distance} target={FormatGrid(targetPos)} invalid-orientation";
                    return false;
                }
            }

            nextState = rollingState;
            return true;
        }

        static bool TryRollDistanceGroundPath(
            DiceState state,
            Direction direction,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            int distance,
            out DiceState nextState,
            out string rejectReason) {
            nextState = default;
            rejectReason = null;

            var rollingState = state;
            for (var step = 0; step < distance; step++) {
                var hasTopOnCell = step == 0 && hasTopOnSameCell;

                if (rollingState.Tier == DiceStackTier.Bottom) {
                    if (hasTopOnCell) {
                        rejectReason = $"step={step + 1}/{distance} has-top-on-start-cell";
                        return false;
                    }

                    var targetPos = rollingState.GridPos + direction.ToGridDelta();
                    var isFinalStep = step == distance - 1;
                    if (!CanEnterRollCell(placement, targetPos, rollingState.Tier, isFinalStep, distance, out var cellReject)) {
                        rejectReason = $"step={step + 1}/{distance} target={FormatGrid(targetPos)} {cellReject}";
                        return false;
                    }

                    if (!TryBuildRolledState(rollingState, targetPos, rollingState.Tier, direction, out rollingState)) {
                        rejectReason = $"step={step + 1}/{distance} target={FormatGrid(targetPos)} invalid-orientation";
                        return false;
                    }

                    continue;
                }

                if (rollingState.Tier == DiceStackTier.Top) {
                    var targetPos = rollingState.GridPos + direction.ToGridDelta();
                    var isFinalStep = step == distance - 1;

                    if (isFinalStep && distance > 1 && placement.CanParallelRollLandAt(targetPos, DiceStackTier.Top)) {
                        if (!TryBuildRolledState(rollingState, targetPos, DiceStackTier.Top, direction, out rollingState)) {
                            rejectReason = $"step={step + 1}/{distance} target={FormatGrid(targetPos)} invalid-orientation";
                            return false;
                        }

                        continue;
                    }

                    if (!SlideResolver.TrySlideTop(rollingState, direction, placement, out var slideState, out var result)
                        || result != TopSlideResult.Parallel) {
                        rejectReason =
                            $"step={step + 1}/{distance} target={FormatGrid(targetPos)} top-slide-failed result={result}";
                        return false;
                    }

                    if (!TryBuildRolledState(rollingState, slideState.GridPos, DiceStackTier.Top, direction, out rollingState)) {
                        rejectReason = $"step={step + 1}/{distance} target={FormatGrid(slideState.GridPos)} invalid-orientation";
                        return false;
                    }

                    continue;
                }

                rejectReason = $"step={step + 1}/{distance} unsupported-tier tier={rollingState.Tier}";
                return false;
            }

            nextState = rollingState;
            return true;
        }

        static bool CanPassJumpRollCell(
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

        static bool CanEnterRollCell(
            IDicePlacement placement,
            Vector2Int targetPos,
            DiceStackTier tier,
            bool isFinalStep,
            int distance,
            out string rejectReason) {
            rejectReason = null;
            if (placement.CanDiceRollInto(targetPos)) {
                return true;
            }

            if (isFinalStep && distance > 1 && placement.CanParallelRollLandAt(targetPos, tier)) {
                return true;
            }

            rejectReason = isFinalStep
                ? $"occupied-final distance={distance} canRollInto=false canParallelLand=false"
                : "occupied-intermediate canRollInto=false";
            return false;
        }

        static bool TryBuildRolledState(
            DiceState state,
            Vector2Int targetPos,
            DiceStackTier tier,
            Direction direction,
            out DiceState nextState) {
            nextState = default;

            var nextOrientation = state.Orientation.Roll(direction);
            if (!nextOrientation.IsValid()) {
                return false;
            }

            nextState = new DiceState(targetPos, nextOrientation, tier);
            return true;
        }

        static string FormatGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
