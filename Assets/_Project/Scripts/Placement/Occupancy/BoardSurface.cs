using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public readonly struct BoardSurface
    {
        public Vector2Int GridCell { get; }
        public int Level { get; }
        public SurfaceState State { get; }
        public DiceController Dice { get; }
        public float SurfaceWorldY { get; }

        BoardSurface(
            Vector2Int gridCell,
            int level,
            SurfaceState state,
            DiceController dice,
            float surfaceWorldY) {
            GridCell = gridCell;
            Level = level;
            State = state;
            Dice = dice;
            SurfaceWorldY = surfaceWorldY;
        }

        public bool IsErasing => State == SurfaceState.Erasing;
        public bool IsSinkErasing => IsErasing && Dice != null && Dice.IsSinkErasing;

        public bool AllowsRoll => Dice != null && State == SurfaceState.Normal;

        public bool AllowsWalkFrom(BoardSurface fromSurface, bool isJumping) {
            return true;
        }

        public static BoardSurface Floor(Vector2Int gridCell, float floorSurfaceWorldY) {
            return new BoardSurface(
                gridCell,
                SurfaceHeightLevel.Floor,
                SurfaceState.Normal,
                null,
                floorSurfaceWorldY);
        }

        public static BoardSurface FromDice(Vector2Int gridCell, int level, DiceController dice) {
            var state = dice != null && dice.IsSinkErasing
                ? SurfaceState.Erasing
                : SurfaceState.Normal;
            var surfaceY = dice != null
                ? dice.GetLogicalTopSurfaceWorldY()
                : 0f;
            return new BoardSurface(gridCell, level, state, dice, surfaceY);
        }

        /// <summary>
        /// Level-2 standing surface on a bottom dice when no top dice exists on the cell.
        /// </summary>
        public static BoardSurface FromDiceAtStackTop(
            Vector2Int gridCell,
            DiceController bottomDice,
            float stackTopSurfaceWorldY) {
            return new BoardSurface(
                gridCell,
                SurfaceHeightLevel.Top,
                bottomDice != null && bottomDice.IsSinkErasing
                    ? SurfaceState.Erasing
                    : SurfaceState.Normal,
                bottomDice,
                stackTopSurfaceWorldY);
        }
    }
}
