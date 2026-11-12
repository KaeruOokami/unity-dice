using DiceGame.Config;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Grid
{
    public class Board : MonoBehaviour, IBoard
    {
        [SerializeField] float cellSize = 1.4f;

        [SerializeField] float floorSurfaceWorldY;

        [Tooltip("World XZ of the board geometric center (midpoint of cell centers).")]
        [SerializeField] Vector2 boardCenterWorldXZ;

        int width = 1;
        int height = 1;
        CellType[,] cells;
        VersusArenaLayout versusLayout;

        public int Width => versusLayout != null ? versusLayout.GlobalWidth : width;
        public int Height => versusLayout != null ? versusLayout.GlobalHeight : height;
        public float CellSize => cellSize;
        public float FloorSurfaceWorldY => floorSurfaceWorldY;
        public Vector2 BoardCenterWorldXZ => boardCenterWorldXZ;
        public VersusArenaLayout VersusLayout => versusLayout;
        public bool IsVersusArena => versusLayout != null;

        void Awake() {
            if (versusLayout == null) {
                InitializeCells();
            }
        }

        void OnValidate() {
            cellSize = Mathf.Max(0.01f, cellSize);
        }

        public void ConfigureStandardArena(int arenaWidth, int arenaHeight) {
            width = Mathf.Max(1, arenaWidth);
            height = Mathf.Max(1, arenaHeight);
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

        /// <summary>
        /// World position of the board geometric center at dice resting height.
        /// </summary>
        public Vector3 GetBoardCenterWorld() {
            return new Vector3(boardCenterWorldXZ.x, cellSize * 0.5f, boardCenterWorldXZ.y);
        }

        /// <summary>
        /// World XZ of cell (0, 0) center, derived from <see cref="BoardCenterWorldXZ"/>.
        /// </summary>
        public Vector2 GetGridOriginWorldXZ() {
            return boardCenterWorldXZ - new Vector2(
                (Width - 1) * cellSize * 0.5f,
                (Height - 1) * cellSize * 0.5f);
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition) {
            var origin = GetGridOriginWorldXZ();
            return new Vector2Int(
                Mathf.RoundToInt((worldPosition.x - origin.x) / cellSize),
                Mathf.RoundToInt((worldPosition.z - origin.y) / cellSize));
        }

        public Vector3 GridToWorld(Vector2Int gridPos) {
            var origin = GetGridOriginWorldXZ();
            var halfHeight = cellSize * 0.5f;
            return new Vector3(
                origin.x + gridPos.x * cellSize,
                halfHeight,
                origin.y + gridPos.y * cellSize);
        }
    }
}
