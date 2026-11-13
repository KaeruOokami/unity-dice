using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public readonly struct DiceSpawnSlot
    {
        public Vector2Int Cell { get; }
        public DiceStackTier Tier { get; }

        public DiceSpawnSlot(Vector2Int cell, DiceStackTier tier) {
            Cell = cell;
            Tier = tier;
        }
    }

    struct SpawnSlotBuckets
    {
        public List<Vector2Int> BottomCells;
        public List<Vector2Int> TopCells;

        public bool HasAny =>
            BottomCells != null && BottomCells.Count > 0
            || TopCells != null && TopCells.Count > 0;
    }

    public static class DiceSpawnCellPicker
    {
        public static bool HasAnySpawnSlot(Board board, DiceRegistry registry) {
            return CollectSpawnBuckets(board, registry, null).HasAny;
        }

        public static List<DiceSpawnSlot> PickRandomSpawnSlots(
            Board board,
            DiceRegistry registry,
            int count,
            float bottomSpawnWeight,
            System.Random random) {
            return PickRandomSpawnSlots(board, registry, null, count, bottomSpawnWeight, random);
        }

        public static List<DiceSpawnSlot> PickRandomSpawnSlots(
            Board board,
            DiceRegistry registry,
            PlayerSlot? ownerSlot,
            int count,
            float bottomSpawnWeight,
            System.Random random) {
            var buckets = CollectSpawnBuckets(board, registry, ownerSlot);
            var results = new List<DiceSpawnSlot>();
            if (board == null || registry == null || count <= 0 || !buckets.HasAny) {
                return results;
            }

            var weight = Mathf.Clamp01(bottomSpawnWeight);
            for (var i = 0; i < count && buckets.HasAny; i++) {
                if (!TryPickWeightedSlot(buckets, weight, random, out var slot)) {
                    break;
                }

                if (!registry.TryReserveSpawnCell(slot.Cell)) {
                    RemoveCellFromBuckets(ref buckets, slot.Cell);
                    i--;
                    continue;
                }

                results.Add(slot);
                RemoveCellFromBuckets(ref buckets, slot.Cell);
            }

            return results;
        }

        public static bool TryPickRandomSpawnSlot(
            Board board,
            DiceRegistry registry,
            float bottomSpawnWeight,
            System.Random random,
            out DiceSpawnSlot slot) {
            return TryPickRandomSpawnSlot(board, registry, null, bottomSpawnWeight, random, out slot);
        }

        public static bool TryPickRandomSpawnSlot(
            Board board,
            DiceRegistry registry,
            PlayerSlot? ownerSlot,
            float bottomSpawnWeight,
            System.Random random,
            out DiceSpawnSlot slot) {
            var slots = PickRandomSpawnSlots(board, registry, ownerSlot, 1, bottomSpawnWeight, random);
            if (slots.Count == 0) {
                slot = default;
                return false;
            }

            slot = slots[0];
            return true;
        }

        public static bool TryPickSequentialAttackSpawnSlot(
            Board board,
            DiceRegistry registry,
            PlayerSlot ownerSlot,
            out DiceSpawnSlot slot) {
            slot = default;
            if (board == null || registry == null || board.VersusLayout == null) {
                return false;
            }

            board.VersusLayout.GetPlayerGridBounds(ownerSlot, out var minCell, out var maxCell);
            var width = maxCell.x - minCell.x + 1;
            var height = maxCell.y - minCell.y + 1;
            var cellCount = width * height;
            if (width <= 0 || height <= 0 || cellCount <= 0) {
                return false;
            }

            // Always scan from the region edge (index 0) so vacated edge cells refill first.
            for (var index = 0; index < cellCount; index++) {
                var x = minCell.x + index % width;
                var y = maxCell.y - index / width;
                var cell = new Vector2Int(x, y);

                DiceStackTier tier;
                if (SpawnCellAvailability.CanSpawnBottomAt(registry, cell)) {
                    tier = DiceStackTier.Bottom;
                } else if (SpawnCellAvailability.CanSpawnTopAt(registry, cell)) {
                    tier = DiceStackTier.Top;
                } else {
                    continue;
                }

                if (!registry.TryReserveSpawnCell(cell)) {
                    continue;
                }

                slot = new DiceSpawnSlot(cell, tier);
                return true;
            }

            return false;
        }

        static SpawnSlotBuckets CollectSpawnBuckets(Board board, DiceRegistry registry, PlayerSlot? ownerSlot) {
            var buckets = new SpawnSlotBuckets {
                BottomCells = new List<Vector2Int>(),
                TopCells = new List<Vector2Int>()
            };

            if (board == null || registry == null) {
                return buckets;
            }

            for (var x = 0; x < board.Width; x++) {
                for (var z = 0; z < board.Height; z++) {
                    var cell = new Vector2Int(x, z);
                    if (ownerSlot.HasValue
                        && board.VersusLayout != null
                        && !board.VersusLayout.IsInsidePlayerRegion(ownerSlot.Value, cell)) {
                        continue;
                    }

                    if (SpawnCellAvailability.CanSpawnBottomAt(registry, cell)) {
                        buckets.BottomCells.Add(cell);
                    }

                    if (SpawnCellAvailability.CanSpawnTopAt(registry, cell)) {
                        buckets.TopCells.Add(cell);
                    }
                }
            }

            return buckets;
        }

        static bool TryPickWeightedSlot(
            SpawnSlotBuckets buckets,
            float bottomSpawnWeight,
            System.Random random,
            out DiceSpawnSlot slot) {
            slot = default;
            var hasBottom = buckets.BottomCells.Count > 0;
            var hasTop = buckets.TopCells.Count > 0;

            if (!hasBottom && !hasTop) {
                return false;
            }

            DiceStackTier tier;
            if (hasBottom && !hasTop) {
                tier = DiceStackTier.Bottom;
            } else if (!hasBottom && hasTop) {
                tier = DiceStackTier.Top;
            } else {
                tier = random.NextDouble() < bottomSpawnWeight
                    ? DiceStackTier.Bottom
                    : DiceStackTier.Top;
            }

            var cells = tier == DiceStackTier.Top ? buckets.TopCells : buckets.BottomCells;
            var index = random.Next(cells.Count);
            slot = new DiceSpawnSlot(cells[index], tier);
            return true;
        }

        static void RemoveCellFromBuckets(ref SpawnSlotBuckets buckets, Vector2Int cell) {
            RemoveCell(buckets.BottomCells, cell);
            RemoveCell(buckets.TopCells, cell);
        }

        static void RemoveCell(List<Vector2Int> cells, Vector2Int cell) {
            if (cells == null) {
                return;
            }

            for (var i = 0; i < cells.Count; i++) {
                if (cells[i] != cell) {
                    continue;
                }

                cells.RemoveAt(i);
                return;
            }
        }

        /// <summary>
        /// Picks a random 2x2 anchor in the player region that stays on floor cells,
        /// fits entirely inside the owner region, and does not overlap blocked player cells.
        /// In-flight spawn and jumbo occupants block; other dice are cleared on landing.
        /// </summary>
        public static bool TryPickJumboSpawnAnchor(
            Board board,
            DiceRegistry registry,
            PlayerSlot targetSlot,
            IReadOnlyList<Vector2Int> blockedCells,
            System.Random random,
            out Vector2Int anchor) {
            anchor = default;
            if (board == null || board.VersusLayout == null || random == null) {
                return false;
            }

            var layout = board.VersusLayout;
            layout.GetPlayerGridBounds(targetSlot, out var minCell, out var maxCell);
            var candidates = new List<Vector2Int>();

            for (var x = minCell.x; x <= maxCell.x - JumboFootprint.Size + 1; x++) {
                for (var y = minCell.y; y <= maxCell.y - JumboFootprint.Size + 1; y++) {
                    var candidate = new Vector2Int(x, y);
                    if (!IsValidJumboAnchor(
                            board,
                            registry,
                            layout,
                            targetSlot,
                            candidate,
                            blockedCells)) {
                        continue;
                    }

                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0) {
                return false;
            }

            anchor = candidates[random.Next(candidates.Count)];
            if (registry == null) {
                return true;
            }

            // Reserve footprint cells so concurrent picks cannot share them.
            var footprint = new List<Vector2Int>(JumboFootprint.CellCount);
            JumboFootprint.AppendCells(anchor, footprint);
            for (var i = 0; i < footprint.Count; i++) {
                if (registry.TryReserveSpawnCell(footprint[i])) {
                    continue;
                }

                for (var j = 0; j < i; j++) {
                    registry.ReleaseSpawnCellReservation(footprint[j]);
                }

                return false;
            }

            return true;
        }

        static bool IsValidJumboAnchor(
            Board board,
            DiceRegistry registry,
            VersusArenaLayout layout,
            PlayerSlot targetSlot,
            Vector2Int anchor,
            IReadOnlyList<Vector2Int> blockedCells) {
            for (var dx = 0; dx < JumboFootprint.Size; dx++) {
                for (var dy = 0; dy < JumboFootprint.Size; dy++) {
                    var cell = new Vector2Int(anchor.x + dx, anchor.y + dy);
                    if (!layout.IsInsidePlayerRegion(targetSlot, cell)
                        || board.GetCell(cell) != CellType.Floor) {
                        return false;
                    }

                    if (registry != null && !CanJumboSpawnOccupyCell(registry, cell)) {
                        return false;
                    }

                    if (blockedCells == null) {
                        continue;
                    }

                    for (var i = 0; i < blockedCells.Count; i++) {
                        if (blockedCells[i] == cell) {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Jumbo spawn blocked by in-flight spawn, erasure, or another jumbo occupant.
        /// </summary>
        static bool CanJumboSpawnOccupyCell(DiceRegistry registry, Vector2Int cell) {
            if (SpawnCellAvailability.HasSpawnBlockerAt(registry, cell)) {
                return false;
            }

            return !IsJumboOccupantAt(registry, cell);
        }

        static bool IsJumboOccupantAt(DiceRegistry registry, Vector2Int cell) {
            if (registry.TryGetBottomAt(cell, out var bottom)
                && IsBlockingJumboOccupant(bottom)) {
                return true;
            }

            if (registry.TryGetTopAt(cell, out var top)
                && IsBlockingJumboOccupant(top)) {
                return true;
            }

            return false;
        }

        static bool IsBlockingJumboOccupant(DiceController dice) {
            return dice != null && dice.Capabilities.HasExpandedFootprint;
        }
    }
}
