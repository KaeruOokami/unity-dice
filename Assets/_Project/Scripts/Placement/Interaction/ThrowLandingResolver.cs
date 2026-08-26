using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Resolves throw landing along a cardinal ray, then picks the farthest placeable cell.
    /// Versus: if the origin is partition-adjacent (1 / -1), the ray may cross and continue
    /// deep into the other field (-2+ / 2+). Otherwise the first cell past the partition
    /// (-1 / 1) is the farthest allowed landing. Solo uses the board rim only.
    /// Distance 0 lands on the origin (in-place hop) when the ray has no travel
    /// or no farther placeable cell.
    /// </summary>
    public static class ThrowLandingResolver
    {
        public static bool TryResolve(
            Vector2Int origin,
            Direction direction,
            Board board,
            IDicePlacement placement,
            PlayerSlot throwerSlot,
            DiceController carriedDice,
            out Vector2Int landingCell,
            out DiceStackTier landingTier,
            out int cellDistance) {
            landingCell = origin;
            landingTier = default;
            cellDistance = 0;

            if (board == null || placement == null) {
                return false;
            }

            var delta = direction.ToGridDelta();
            if (delta == Vector2Int.zero) {
                return TryResolveOriginLanding(origin, board, placement, carriedDice, out landingTier);
            }

            var versusLayout = board.VersusLayout;
            var originOnPartitionEdge = versusLayout != null
                && IsPartitionAdjacent(origin, versusLayout);

            var edgeCell = origin;
            var steps = 0;
            while (true) {
                var next = edgeCell + delta;
                if (!board.IsInside(next) || board.GetCell(next) != CellType.Floor) {
                    break;
                }

                if (versusLayout != null) {
                    if (versusLayout.CrossesPartition(edgeCell, next)) {
                        edgeCell = next;
                        steps++;
                        // From 2/3/... / -2/... : stop on the first cell past the partition.
                        // From 1/-1 : keep going into the far field.
                        if (!originOnPartitionEdge) {
                            break;
                        }

                        continue;
                    }

                    if (!versusLayout.IsInsidePlayerRegion(throwerSlot, next)) {
                        // Foreign cells: allow when origin was partition-adjacent (deep cross),
                        // or when already on the foreign side (walk toward/away from partition).
                        if (originOnPartitionEdge
                            || !versusLayout.IsInsidePlayerRegion(throwerSlot, edgeCell)) {
                            edgeCell = next;
                            steps++;
                            continue;
                        }

                        break;
                    }
                }

                edgeCell = next;
                steps++;
            }

            if (steps > 0) {
                var candidate = edgeCell;
                for (var remaining = steps; remaining > 0; remaining--) {
                    if (CarryPlacementPassability.TryResolveTarget(
                        candidate,
                        placement,
                        out landingTier,
                        out _)) {
                        landingCell = candidate;
                        cellDistance = remaining;
                        return true;
                    }

                    candidate -= delta;
                }
            }

            landingCell = origin;
            cellDistance = 0;
            return TryResolveOriginLanding(origin, board, placement, carriedDice, out landingTier);
        }

        static bool IsPartitionAdjacent(Vector2Int cell, VersusArenaLayout layout) {
            var directions = new[] {
                Direction.East,
                Direction.West,
                Direction.North,
                Direction.South
            };
            for (var i = 0; i < directions.Length; i++) {
                var next = cell + directions[i].ToGridDelta();
                if (layout.IsInsideGlobal(next) && layout.CrossesPartition(cell, next)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Origin landing as if <paramref name="carriedDice"/> has already vacated the cell.
        /// </summary>
        static bool TryResolveOriginLanding(
            Vector2Int origin,
            Board board,
            IDicePlacement placement,
            DiceController carriedDice,
            out DiceStackTier landingTier) {
            landingTier = default;

            if (board == null || !board.IsInside(origin) || board.GetCell(origin) != CellType.Floor) {
                return false;
            }

            if (placement is DiceRegistry registry) {
                var bottomIsOther = registry.TryGetBottomAt(origin, out var bottom)
                    && bottom != null
                    && bottom != carriedDice;
                var topIsOther = registry.TryGetTopAt(origin, out var top)
                    && top != null
                    && top != carriedDice;

                if (!bottomIsOther) {
                    landingTier = DiceStackTier.Bottom;
                    return true;
                }

                if (!topIsOther) {
                    landingTier = DiceStackTier.Top;
                    return true;
                }

                return false;
            }

            return CarryPlacementPassability.TryResolveTarget(origin, placement, out landingTier, out _);
        }
    }
}
