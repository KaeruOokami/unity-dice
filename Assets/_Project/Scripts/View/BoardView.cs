using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.View
{
    [RequireComponent(typeof(Board))]
    public class BoardView : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] Material floorMaterial;
        [SerializeField] bool generateFloorTiles = true;

        void Awake() {
            if (board == null) {
                board = GetComponent<Board>();
            }

            if (generateFloorTiles) {
                BuildFloorPlane();
            }
        }

        void BuildFloorPlane() {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(transform, false);

            var boardWidth = board.Width * board.CellSize;
            var boardDepth = board.Height * board.CellSize;
            var centerX = (board.Width - 1) * board.CellSize * 0.5f;
            var centerZ = (board.Height - 1) * board.CellSize * 0.5f;

            floor.transform.localPosition = new Vector3(
                centerX,
                board.FloorSurfaceWorldY,
                centerZ);
            floor.transform.localScale = new Vector3(
                boardWidth / 10f,
                1f,
                boardDepth / 10f);

            if (floorMaterial != null) {
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            }
        }
    }
}
