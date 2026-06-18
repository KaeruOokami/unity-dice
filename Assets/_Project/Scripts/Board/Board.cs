using UnityEngine;

namespace DiceGame.Grid
{
    public class Board : MonoBehaviour, IBoard
    {
        [SerializeField] int width = 5;
        [SerializeField] int height = 5;
        [SerializeField] float cellSize = 1.4f;

        CellType[,] cells;
        Vector2Int? dicePosition;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector2Int? DicePosition => dicePosition;

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

        public bool CanEnter(Vector2Int gridPos) {
            if (!IsInside(gridPos) || GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            if (dicePosition.HasValue && dicePosition.Value == gridPos) {
                return false;
            }

            return true;
        }

        public Vector3 GridToWorld(Vector2Int gridPos) {
            var halfHeight = cellSize * 0.5f;
            return new Vector3(gridPos.x * cellSize, halfHeight, gridPos.y * cellSize);
        }

        public void SetDicePosition(Vector2Int? gridPos) {
            dicePosition = gridPos;
        }
    }
}
