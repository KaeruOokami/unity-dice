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
            SurfaceLayer layer,
            DiceController standingDice,
            DiceStackTier standingTier) {
            if (layer == SurfaceLayer.Floor) {
                return BoardSurface.Floor(gridCell, board.FloorSurfaceWorldY);
            }

            if (standingDice == null) {
                return BoardSurface.Floor(gridCell, board.FloorSurfaceWorldY);
            }

            if (layer == SurfaceLayer.Top
                && standingTier == DiceStackTier.Bottom
                && standingDice.CurrentState.Tier == DiceStackTier.Bottom
                && !registry.HasTopAt(gridCell)) {
                return BoardSurface.VirtualStackTop(
                    gridCell,
                    standingDice,
                    GetStackTopStandingSurfaceY(standingDice));
            }

            return BoardSurface.FromDice(gridCell, layer, standingDice);
        }

        public bool TryGetSurfaceAt(Vector2Int gridCell, SurfaceLayer layer, out BoardSurface surface) {
            if (!board.IsInside(gridCell) || board.GetCell(gridCell) == CellType.Wall) {
                surface = default;
                return false;
            }

            if (layer == SurfaceLayer.Floor) {
                surface = BoardSurface.Floor(gridCell, board.FloorSurfaceWorldY);
                return true;
            }

            if (layer == SurfaceLayer.Top) {
                if (registry.TryGetTopAt(gridCell, out var topDice) && topDice != null) {
                    surface = BoardSurface.FromDice(gridCell, SurfaceLayer.Top, topDice);
                    return true;
                }

                if (registry.TryGetBottomAt(gridCell, out var bottomForTop) && bottomForTop != null) {
                    surface = BoardSurface.VirtualStackTop(
                        gridCell,
                        bottomForTop,
                        GetStackTopStandingSurfaceY(bottomForTop));
                    return true;
                }

                surface = default;
                return false;
            }

            if (registry.TryGetBottomAt(gridCell, out var bottomDice) && bottomDice != null) {
                surface = BoardSurface.FromDice(gridCell, SurfaceLayer.Bottom, bottomDice);
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
                return top.GetTopSurfaceWorldY();
            }

            return bottomDice.GetTopSurfaceWorldY() + board.CellSize;
        }
    }
}
