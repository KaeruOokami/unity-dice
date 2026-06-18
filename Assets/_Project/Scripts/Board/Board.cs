using System.Collections.Generic;
using UnityEngine;

namespace DiceGame.Grid
{
    public class Board : MonoBehaviour, IBoard
    {
        [SerializeField] int width = 5;
        [SerializeField] int height = 5;
        [SerializeField] float cellSize = 1.4f;

        CellType[,] cells;
        readonly HashSet<Vector2Int> diceCells = new();

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;

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
            diceCells.Clear();
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

        public bool HasDiceAt(Vector2Int gridPos) {
            return diceCells.Contains(gridPos);
        }

        public bool CanDiceRollInto(Vector2Int gridPos) {
            if (!IsInside(gridPos) || GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            return !HasDiceAt(gridPos);
        }

        public void RegisterDice(Vector2Int gridPos) {
            diceCells.Add(gridPos);
        }

        public void MoveDice(Vector2Int from, Vector2Int to) {
            diceCells.Remove(from);
            diceCells.Add(to);
        }

        public Vector3 GridToWorld(Vector2Int gridPos) {
            var halfHeight = cellSize * 0.5f;
            return new Vector3(gridPos.x * cellSize, halfHeight, gridPos.y * cellSize);
        }
    }
}
