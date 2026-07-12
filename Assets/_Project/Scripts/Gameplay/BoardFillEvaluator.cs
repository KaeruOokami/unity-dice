using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public static class BoardFillEvaluator
    {
        public static bool IsStandardBottomFull(Board board, DiceRegistry registry)
        {
            if (board == null || registry == null)
            {
                return false;
            }

            var foundFloorCell = false;
            for (var x = 0; x < board.Width; x++)
            {
                for (var z = 0; z < board.Height; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (board.GetCell(cell) != CellType.Floor)
                    {
                        continue;
                    }

                    foundFloorCell = true;
                    if (!HasActiveDiceAt(registry, cell, DiceStackTier.Bottom))
                    {
                        return false;
                    }
                }
            }

            return foundFloorCell;
        }

        public static bool IsVersusRegionFull(
            Board board,
            DiceRegistry registry,
            PlayerSlot playerSlot)
        {
            if (board == null || registry == null || board.VersusLayout == null)
            {
                return false;
            }

            board.VersusLayout.GetPlayerGridBounds(playerSlot, out var minCell, out var maxCell);
            var foundFloorCell = false;
            for (var x = minCell.x; x <= maxCell.x; x++)
            {
                for (var z = minCell.y; z <= maxCell.y; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (board.GetCell(cell) != CellType.Floor)
                    {
                        continue;
                    }

                    foundFloorCell = true;
                    if (!HasActiveDiceAt(registry, cell, DiceStackTier.Bottom)
                        || !HasActiveDiceAt(registry, cell, DiceStackTier.Top))
                    {
                        return false;
                    }
                }
            }

            return foundFloorCell;
        }

        static bool HasActiveDiceAt(
            DiceRegistry registry,
            Vector2Int cell,
            DiceStackTier tier)
        {
            return registry.TryGetDiceAt(cell, tier, out var dice)
                && dice != null
                && !dice.IsErasing;
        }
    }
}
