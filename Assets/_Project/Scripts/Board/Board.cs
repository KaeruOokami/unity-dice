using DiceGame.Config;
using DiceGame.Grid;
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
        VersusArenaLayout versusLayout;

        public int Width => versusLayout != null ? versusLayout.GlobalWidth : width;
        public int Height => versusLayout != null ? versusLayout.GlobalHeight : height;
        public float CellSize => cellSize;
        public float FloorSurfaceWorldY => floorSurfaceWorldY;
        public VersusArenaLayout VersusLayout => versusLayout;
        public bool IsVersusArena => versusLayout != null;

        void Awake() {
            if (versusLayout == null) {
                InitializeCells();
            }
        }

        void OnValidate() {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = Mathf.Max(0.01f, cellSize);
        }

        public void ConfigureStandardArena() {
            versusLayout = null;
            InitializeCells();
        }

        public void ConfigureVersusArena(VersusArenaLayout layout) {
            versusLayout = layout;
            InitializeCells();
        }

        public void InitializeCells() {
            var arenaWidth = Width;
            var arenaHeight = Height;
            cells = new CellType[arenaWidth, arenaHeight];
            for (var x = 0; x < arenaWidth; x++) {
                for (var z = 0; z < arenaHeight; z++) {
                    var gridPos = new Vector2Int(x, z);
                    cells[x, z] = versusLayout != null
                        ? versusLayout.GetCell(gridPos)
                        : CellType.Floor;
                }
            }
        }

        public bool IsInside(Vector2Int gridPos) {
            if (versusLayout != null) {
                return versusLayout.IsInsideGlobal(gridPos);
            }

            return gridPos.x >= 0 && gridPos.x < width && gridPos.y >= 0 && gridPos.y < height;
        }

        public CellType GetCell(Vector2Int gridPos) {
            if (versusLayout != null) {
                return versusLayout.GetCell(gridPos);
            }

            return IsInside(gridPos) ? cells[gridPos.x, gridPos.y] : CellType.Wall;
        }

        public bool BlocksMovement(Vector2Int fromCell, Vector2Int toCell, PlayerSlot? movementOwner) {
            return PartitionBoundaryPolicy.BlocksMovement(versusLayout, fromCell, toCell, movementOwner);
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
