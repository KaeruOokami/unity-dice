using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class GridTraversability
    {
        public static bool CanTraverseCell(
            CellOccupancyQuery query,
            DiceStackTier fromTier,
            Vector2Int cell,
            out string rejectReason) {
            rejectReason = null;
            var fromRank = CellOccupancyQuery.ToTierRank(fromTier);

            if (!query.TryGetOccupancyTier(cell, out var occupancy)) {
                rejectReason = $"cell={FormatGrid(cell)} invalid-or-blocked";
                return false;
            }

            if ((int)occupancy >= fromRank) {
                rejectReason =
                    $"cell={FormatGrid(cell)} occupancy={occupancy} not-below fromTier={fromTier} rank={fromRank}";
                return false;
            }

            return true;
        }

        public static bool TryEvaluateRollPath(
            CellOccupancyQuery query,
            DiceStackTier fromTier,
            Vector2Int fromCell,
            Direction direction,
            int distance,
            bool allowUpwardTier,
            out DiceStackTier landingTier,
            out string rejectReason) {
            landingTier = default;
            rejectReason = null;

            if (distance < 1) {
                rejectReason = "distance-too-small";
                return false;
            }

            for (var step = 1; step < distance; step++) {
                var pathCell = fromCell + direction.ToGridDelta() * step;
                var previousCell = fromCell + direction.ToGridDelta() * (step - 1);
                if (query.BlocksRollBetween(previousCell, pathCell)) {
                    rejectReason = $"traverse step={step}/{distance} blocked-by-partition cell={FormatGrid(pathCell)}";
                    return false;
                }

                if (!CanTraverseCell(query, fromTier, pathCell, out rejectReason)) {
                    rejectReason = $"traverse step={step}/{distance} {rejectReason}";
                    return false;
                }
            }

            var landingCell = fromCell + direction.ToGridDelta() * distance;
            var previousLandingCell = fromCell + direction.ToGridDelta() * (distance - 1);
            if (query.BlocksRollBetween(previousLandingCell, landingCell)) {
                rejectReason = $"land step={distance}/{distance} blocked-by-partition cell={FormatGrid(landingCell)}";
                return false;
            }

            if (!CanLandAt(query, fromTier, landingCell, allowUpwardTier, out landingTier, out rejectReason)) {
                rejectReason = $"land step={distance}/{distance} {rejectReason}";
                return false;
            }

            return true;
        }

        public static bool CanLandAt(
            CellOccupancyQuery query,
            DiceStackTier fromTier,
            Vector2Int cell,
            bool allowUpwardTier,
            out DiceStackTier landingTier,
            out string rejectReason) {
            landingTier = default;
            rejectReason = null;
            var fromRank = CellOccupancyQuery.ToTierRank(fromTier);

            if (!query.TryResolveLandingTier(fromTier, cell, out landingTier)) {
                rejectReason = $"cell={FormatGrid(cell)} no-valid-landing fromTier={fromTier}";
                return false;
            }

            var landingRank = CellOccupancyQuery.ToTierRank(landingTier);
            if (landingRank > fromRank) {
                if (!allowUpwardTier) {
                    rejectReason =
                        $"cell={FormatGrid(cell)} landingTier={landingTier} rank={landingRank} above fromTier={fromTier} rank={fromRank}";
                    return false;
                }

                return true;
            }

            if (!CanTraverseCell(query, fromTier, cell, out rejectReason)) {
                rejectReason = $"cell={FormatGrid(cell)} land-occupancy {rejectReason}";
                return false;
            }

            return true;
        }

        public static DiceGridMoveKind ResolveMoveKind(DiceStackTier fromTier, DiceStackTier toTier) {
            if (fromTier == toTier) {
                return DiceGridMoveKind.Parallel;
            }

            return fromTier == DiceStackTier.Top
                ? DiceGridMoveKind.Demote
                : DiceGridMoveKind.Stack;
        }

        static string FormatGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
