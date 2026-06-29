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

        public static bool CanLandAt(
            CellOccupancyQuery query,
            DiceStackTier fromTier,
            Vector2Int cell,
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
                rejectReason =
                    $"cell={FormatGrid(cell)} landingTier={landingTier} rank={landingRank} above fromTier={fromTier} rank={fromRank}";
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
                if (!CanTraverseCell(query, fromTier, pathCell, out rejectReason)) {
                    rejectReason = $"traverse step={step}/{distance} {rejectReason}";
                    return false;
                }
            }

            var landingCell = fromCell + direction.ToGridDelta() * distance;
            if (!CanLandAt(query, fromTier, landingCell, out landingTier, out rejectReason)) {
                rejectReason = $"land step={distance}/{distance} {rejectReason}";
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
