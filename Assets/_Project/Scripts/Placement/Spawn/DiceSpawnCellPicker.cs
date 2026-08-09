using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.SimShared.Spawn;
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

    /// <summary>
    /// Unity adapter over shared <see cref="SpawnSlotPicker"/> (single algorithm with Quantum).
    /// </summary>
    public static class DiceSpawnCellPicker
    {
        public static bool HasAnySpawnSlot(Board board, DiceRegistry registry) {
            if (board == null || registry == null) {
                return false;
            }

            var query = new RegistrySpawnBoardQuery(board, registry, null);
            for (var y = 0; y < query.Height; y++) {
                for (var x = 0; x < query.Width; x++) {
                    if (!query.IsCellAllowed(x, y)) {
                        continue;
                    }

                    if (query.CanPlaceBottom(x, y) || query.CanPlaceTop(x, y)) {
                        return true;
                    }
                }
            }

            return false;
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
            var results = new List<DiceSpawnSlot>();
            if (board == null || registry == null || count <= 0 || random == null) {
                return results;
            }

            var buffer = new SpawnCellSlot[count];
            var query = new RegistrySpawnBoardQuery(board, registry, ownerSlot);
            var n = SpawnSlotPicker.PickRandomSlots(
                query,
                count,
                bottomSpawnWeight,
                random.Next,
                buffer);
            for (var i = 0; i < n; i++) {
                results.Add(ToUnitySlot(buffer[i]));
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
            slot = default;
            if (board == null || registry == null || random == null) {
                return false;
            }

            var query = new RegistrySpawnBoardQuery(board, registry, ownerSlot);
            if (!SpawnSlotPicker.TryPickRandomSlot(query, bottomSpawnWeight, random.Next, out var shared)) {
                return false;
            }

            slot = ToUnitySlot(shared);
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

            // Region-local edge-first scan (Versus). Shared full-board attack pick is for Quantum.
            for (var index = 0; index < cellCount; index++) {
                var x = minCell.x + index % width;
                var y = maxCell.y - index / width;
                var cell = new Vector2Int(x, y);

                if (registry.HasErasingDiceAt(cell)) {
                    continue;
                }

                if (registry.CanPlaceBottomDiceAt(cell)) {
                    slot = new DiceSpawnSlot(cell, DiceStackTier.Bottom);
                    return true;
                }

                if (registry.CanPlaceTopDiceAt(cell)) {
                    slot = new DiceSpawnSlot(cell, DiceStackTier.Top);
                    return true;
                }
            }

            return false;
        }

        static DiceSpawnSlot ToUnitySlot(SpawnCellSlot shared) {
            return new DiceSpawnSlot(
                new Vector2Int(shared.X, shared.Y),
                shared.IsTop ? DiceStackTier.Top : DiceStackTier.Bottom);
        }

        sealed class RegistrySpawnBoardQuery : ISpawnBoardQuery
        {
            readonly Board board;
            readonly DiceRegistry registry;
            readonly PlayerSlot? ownerSlot;

            public RegistrySpawnBoardQuery(Board board, DiceRegistry registry, PlayerSlot? ownerSlot) {
                this.board = board;
                this.registry = registry;
                this.ownerSlot = ownerSlot;
            }

            public int Width => board.Width;
            public int Height => board.Height;

            public bool IsCellAllowed(int x, int y) {
                var cell = new Vector2Int(x, y);
                if (ownerSlot.HasValue
                    && board.VersusLayout != null
                    && !board.VersusLayout.IsInsidePlayerRegion(ownerSlot.Value, cell)) {
                    return false;
                }

                return !registry.HasErasingDiceAt(cell);
            }

            public bool CanPlaceBottom(int x, int y) {
                return registry.CanPlaceBottomDiceAt(new Vector2Int(x, y));
            }

            public bool CanPlaceTop(int x, int y) {
                return registry.CanPlaceTopDiceAt(new Vector2Int(x, y));
            }
        }

        /// <summary>
        /// Picks a random 2x2 anchor in the player region that stays on floor cells,
        /// fits entirely inside the owner region, and does not overlap blocked player cells.
        /// Only pending spawns and jumbo occupants (including mid-erasure) block a cell; other dice are
        /// treated as free space and cleared on landing.
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

        static bool CanJumboSpawnOccupyCell(DiceRegistry registry, Vector2Int cell) {
            if (registry.HasPendingBottomAt(cell) || registry.HasPendingTopAt(cell)) {
                return false;
            }

            if (IsJumboOccupantAt(registry, cell)) {
                return false;
            }

            return true;
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
