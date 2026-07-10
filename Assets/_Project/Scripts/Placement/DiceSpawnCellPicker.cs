using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
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

                results.Add(slot);
                RemoveSlot(ref buckets, slot);
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

                    if (registry.HasDissolvingDiceAt(cell)) {
                        continue;
                    }

                    if (registry.CanPlaceBottomDiceAt(cell)) {
                        buckets.BottomCells.Add(cell);
                    }

                    if (registry.CanPlaceTopDiceAt(cell)) {
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

        static void RemoveSlot(ref SpawnSlotBuckets buckets, DiceSpawnSlot slot) {
            var cells = slot.Tier == DiceStackTier.Top ? buckets.TopCells : buckets.BottomCells;
            for (var i = 0; i < cells.Count; i++) {
                if (cells[i] != slot.Cell) {
                    continue;
                }

                cells.RemoveAt(i);
                return;
            }
        }
    }
}
