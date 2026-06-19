using System.Collections.Generic;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Grid
{
    public class Board : MonoBehaviour, IBoard
    {
        [SerializeField] int width = 5;
        [SerializeField] int height = 5;
        [SerializeField] float cellSize = 1.4f;

        [SerializeField] float floorSurfaceWorldY;

        CellType[,] cells;
        readonly HashSet<Vector2Int> bottomDiceCells = new();
        readonly HashSet<Vector2Int> topDiceCells = new();

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public float FloorSurfaceWorldY => floorSurfaceWorldY;

        void Awake() {
            InitializeCells();
        }

        void OnValidate() {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = Mathf.Max(0.01f, cellSize);
        }

        public void InitializeCells() {
            cells = new CellType[width, height];
            bottomDiceCells.Clear();
            topDiceCells.Clear();
            for (var x = 0; x < width; x++) {
                for (var z = 0; z < height; z++) {
                    cells[x, z] = CellType.Floor;
                }
            }
        }

        public bool IsInside(Vector2Int gridPos) {
            return gridPos.x >= 0 && gridPos.x < width && gridPos.y >= 0 && gridPos.y < height;
        }

        public CellType GetCell(Vector2Int gridPos) {
            return IsInside(gridPos) ? cells[gridPos.x, gridPos.y] : CellType.Wall;
        }

        public bool HasBottomDiceAt(Vector2Int gridPos) {
            return bottomDiceCells.Contains(gridPos);
        }

        public bool HasTopDiceAt(Vector2Int gridPos) {
            return topDiceCells.Contains(gridPos);
        }

        public bool HasDiceAt(Vector2Int gridPos) {
            return HasBottomDiceAt(gridPos) || HasTopDiceAt(gridPos);
        }

        public bool CanPlaceBottomDiceAt(Vector2Int gridPos) {
            if (!IsInside(gridPos) || GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            return !HasBottomDiceAt(gridPos);
        }

        public bool CanPlaceTopDiceAt(Vector2Int gridPos) {
            if (!IsInside(gridPos) || GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            return HasBottomDiceAt(gridPos) && !HasTopDiceAt(gridPos);
        }

        public bool CanDiceRollInto(Vector2Int gridPos) {
            return CanPlaceBottomDiceAt(gridPos);
        }

        public void RegisterDice(Vector2Int gridPos, DiceStackTier tier) {
            if (tier == DiceStackTier.Top) {
                topDiceCells.Add(gridPos);
            } else {
                bottomDiceCells.Add(gridPos);
            }
        }

        public void MoveDice(Vector2Int from, Vector2Int to, DiceStackTier tier) {
            UnregisterDice(from, tier);
            RegisterDice(to, tier);
        }

        public void UnregisterDice(Vector2Int gridPos, DiceStackTier tier) {
            if (tier == DiceStackTier.Top) {
                topDiceCells.Remove(gridPos);
            } else {
                bottomDiceCells.Remove(gridPos);
            }
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition) {
            return new Vector2Int(
                Mathf.RoundToInt(worldPosition.x / cellSize),
                Mathf.RoundToInt(worldPosition.z / cellSize));
        }

        public Vector3 GridToWorld(Vector2Int gridPos) {
            var halfHeight = cellSize * 0.5f;
            return new Vector3(gridPos.x * cellSize, halfHeight, gridPos.y * cellSize);
        }
    }
}
