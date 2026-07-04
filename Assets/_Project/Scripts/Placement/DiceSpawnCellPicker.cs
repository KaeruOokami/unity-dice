using System.Collections.Generic;
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

    public static class DiceSpawnCellPicker
    {
        public static bool HasAnySpawnSlot(Board board, DiceRegistry registry) {
            return CollectSpawnSlots(board, registry).Count > 0;
        }

        public static List<DiceSpawnSlot> PickRandomSpawnSlots(
            Board board,
            DiceRegistry registry,
            int count,
            System.Random random) {
            var slots = CollectSpawnSlots(board, registry);
            if (board == null || registry == null || count <= 0 || slots.Count == 0) {
                return new List<DiceSpawnSlot>();
            }

            for (var i = slots.Count - 1; i > 0; i--) {
                var j = random.Next(i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }

            var take = Mathf.Min(count, slots.Count);
            return slots.GetRange(0, take);
        }

        public static bool TryPickRandomSpawnSlot(
            Board board,
            DiceRegistry registry,
            System.Random random,
            out DiceSpawnSlot slot) {
            var slots = PickRandomSpawnSlots(board, registry, 1, random);
            if (slots.Count == 0) {
                slot = default;
                return false;
            }

            slot = slots[0];
            return true;
        }

        static List<DiceSpawnSlot> CollectSpawnSlots(Board board, DiceRegistry registry) {
            var slots = new List<DiceSpawnSlot>();
            if (board == null || registry == null) {
                return slots;
            }

            for (var x = 0; x < board.Width; x++) {
                for (var z = 0; z < board.Height; z++) {
                    var cell = new Vector2Int(x, z);
                    if (registry.HasDissolvingDiceAt(cell)) {
                        continue;
                    }

                    if (registry.CanPlaceBottomDiceAt(cell)) {
                        slots.Add(new DiceSpawnSlot(cell, DiceStackTier.Bottom));
                    }

                    if (registry.CanPlaceTopDiceAt(cell)) {
                        slots.Add(new DiceSpawnSlot(cell, DiceStackTier.Top));
                    }
                }
            }

            return slots;
        }
    }
}
