using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class JumpGridPassability
    {
        public static bool TryEvaluate(
            DiceState fromState,
            Direction direction,
            int distance,
            IDicePlacement placement,
            bool hasTopOnSameCell,
            PassabilityContext context,
            out DiceStackTier landingTier,
            out DiceGridMoveKind moveKind,
            out string rejectReason) {
            landingTier = default;
            moveKind = default;
            rejectReason = null;

            if (!context.IsJumping) {
                rejectReason = "not-jumping";
                return false;
            }

            if (!context.AllowJumpGridMove) {
                rejectReason = "jump-grid-move-not-allowed";
                return false;
            }

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
                var isFinalStep = step == distance;
                if (!CanPassPathCell(
                    placement,
                    fromState.Tier,
                    pathCell,
                    isFinalStep,
                    distance,
                    out var cellReject)) {
                    rejectReason = $"step={step}/{distance} target={FormatGrid(pathCell)} {cellReject}";
                    return false;
                }
            }

            if (!TryResolveLandingTier(
                fromState.Tier,
                landingCell,
                placement,
                distance,
                out landingTier,
                out var tierReject)) {
                rejectReason = $"landing={FormatGrid(landingCell)} {tierReject}";
                return false;
            }

            moveKind = ResolveMoveKind(fromState.Tier, landingTier);
            if (!IsMoveKindAllowed(moveKind, distance, context.AllowJumpTierChange, out var policyReject)) {
                rejectReason = policyReject;
                return false;
            }

            return true;
        }

        static bool IsMoveKindAllowed(
            DiceGridMoveKind moveKind,
            int distance,
            bool allowTierChange,
            out string rejectReason) {
            rejectReason = null;

            if (moveKind == DiceGridMoveKind.Parallel) {
                return true;
            }

            if (distance > 1) {
                rejectReason = $"tier-change-not-allowed-at-distance distance={distance} kind={moveKind}";
                return false;
            }

            if (!allowTierChange) {
                rejectReason = $"tier-change-not-allowed kind={moveKind}";
                return false;
            }

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
            int distance,
            out DiceStackTier landingTier,
            out string rejectReason) {
            landingTier = default;
            rejectReason = null;

            if (distance > 1) {
                if (fromTier == DiceStackTier.Bottom) {
                    if (placement.CanPlaceBottomDiceAt(landingCell)) {
                        landingTier = DiceStackTier.Bottom;
                        return true;
                    }

                    rejectReason = "multi-cell-bottom-requires-empty-landing";
                    return false;
                }

                if (placement.CanPlaceTopDiceAt(landingCell)) {
                    landingTier = DiceStackTier.Top;
                    return true;
                }

                rejectReason = "multi-cell-top-requires-stack-base-landing";
                return false;
            }

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

        static bool CanPassPathCell(
            IDicePlacement placement,
            DiceStackTier tier,
            Vector2Int targetPos,
            bool isFinalStep,
            int distance,
            out string rejectReason) {
            rejectReason = null;

            if (placement.CanDiceRollInto(targetPos)) {
                return true;
            }

            if (tier == DiceStackTier.Bottom) {
                if (isFinalStep && distance == 1 && placement.CanPlaceTopDiceAt(targetPos)) {
                    return true;
                }

                rejectReason = "bottom-path-blocked";
                return false;
            }

            if (placement.CanPlaceTopDiceAt(targetPos)) {
                return true;
            }

            rejectReason = "top-path-blocked";
            return false;
        }

        static string FormatGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
