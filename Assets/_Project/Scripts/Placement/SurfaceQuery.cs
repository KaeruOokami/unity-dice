using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public sealed class SurfaceQuery
    {
        readonly Board board;
        readonly DiceRegistry registry;

        public SurfaceQuery(Board board, DiceRegistry registry) {
            this.board = board;
            this.registry = registry;
        }

        public BoardSurface GetStandingSurface(
            Vector2Int gridCell,
            int fromLevel,
            DiceController standingDice) {
            if (SurfaceHeightLevel.IsFloor(fromLevel)) {
                return BoardSurface.Floor(gridCell, board.FloorSurfaceWorldY);
            }

            if (standingDice == null) {
                return BoardSurface.Floor(gridCell, board.FloorSurfaceWorldY);
            }

            return BoardSurface.FromDice(gridCell, fromLevel, standingDice);
        }

        public bool TryGetSurfaceAt(Vector2Int gridCell, int level, out BoardSurface surface) {
            if (!board.IsInside(gridCell) || board.GetCell(gridCell) == CellType.Wall) {
                surface = default;
                return false;
            }

            if (SurfaceHeightLevel.IsFloor(level)) {
                surface = BoardSurface.Floor(gridCell, board.FloorSurfaceWorldY);
                return true;
            }

            if (SurfaceHeightLevel.IsAtOrAboveTop(level)) {
                if (registry.TryGetTopAt(gridCell, out var topDice) && topDice != null) {
                    surface = BoardSurface.FromDice(gridCell, SurfaceHeightLevel.Top, topDice);
                    return true;
                }

                if (registry.TryGetBottomAt(gridCell, out var bottomForTop) && bottomForTop != null) {
                    surface = BoardSurface.FromDiceAtStackTop(
                        gridCell,
                        bottomForTop,
                        GetStackTopStandingSurfaceY(bottomForTop));
                    return true;
                }

                surface = default;
                return false;
            }

            if (registry.TryGetBottomAt(gridCell, out var bottomDice) && bottomDice != null) {
                surface = BoardSurface.FromDice(gridCell, SurfaceHeightLevel.Bottom, bottomDice);
                return true;
            }

            surface = default;
            return false;
        }

        public float GetStackTopStandingSurfaceY(DiceController bottomDice) {
            if (bottomDice == null) {
                return board.FloorSurfaceWorldY;
            }

            if (registry.TryGetTopAt(bottomDice.CurrentState.GridPos, out var top) && top != null) {
                return top.GetLogicalTopSurfaceWorldY();
            }

            return bottomDice.GetLogicalTopSurfaceWorldY() + board.CellSize;
        }
    }
}
