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
