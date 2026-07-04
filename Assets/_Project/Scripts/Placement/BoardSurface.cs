using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public readonly struct BoardSurface
    {
        public Vector2Int GridCell { get; }
        public SurfaceLayer Layer { get; }
        public SurfaceState State { get; }
        public DiceController Dice { get; }
        public float SurfaceWorldY { get; }

        BoardSurface(
            Vector2Int gridCell,
            SurfaceLayer layer,
            SurfaceState state,
            DiceController dice,
            float surfaceWorldY) {
            GridCell = gridCell;
            Layer = layer;
            State = state;
            Dice = dice;
            SurfaceWorldY = surfaceWorldY;
        }

        public bool IsDissolving => State == SurfaceState.Dissolving;

        public bool AllowsRoll => Dice != null && State == SurfaceState.Normal;

        public bool AllowsWalkFrom(BoardSurface fromSurface, bool isJumping) {
            return true;
        }

        public static BoardSurface Floor(Vector2Int gridCell, float floorSurfaceWorldY) {
            return new BoardSurface(
                gridCell,
                SurfaceLayer.Floor,
                SurfaceState.Normal,
                null,
                floorSurfaceWorldY);
        }

        public static BoardSurface FromDice(Vector2Int gridCell, SurfaceLayer layer, DiceController dice) {
            var state = dice != null && dice.IsDissolving
                ? SurfaceState.Dissolving
                : SurfaceState.Normal;
            var surfaceY = dice != null
                ? dice.GetLogicalTopSurfaceWorldY()
                : 0f;
            return new BoardSurface(gridCell, layer, state, dice, surfaceY);
        }

        public static BoardSurface VirtualStackTop(
            Vector2Int gridCell,
            DiceController bottomDice,
            float stackTopSurfaceWorldY) {
            return new BoardSurface(
                gridCell,
                SurfaceLayer.Top,
                bottomDice != null && bottomDice.IsDissolving
                    ? SurfaceState.Dissolving
                    : SurfaceState.Normal,
                bottomDice,
                stackTopSurfaceWorldY);
        }
    }
}
