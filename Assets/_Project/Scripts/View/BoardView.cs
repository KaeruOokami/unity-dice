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
                BuildFloorTiles();
            }
        }

        void BuildFloorTiles() {
            var parent = new GameObject("FloorTiles").transform;
            parent.SetParent(transform, false);

            var tileHeight = 0.1f;
            var tileScale = new Vector3(board.CellSize * 0.95f, tileHeight, board.CellSize * 0.95f);

            for (var x = 0; x < board.Width; x++) {
                for (var z = 0; z < board.Height; z++) {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Floor_{x}_{z}";
                    tile.transform.SetParent(parent, false);
                    tile.transform.localScale = tileScale;
                    tile.transform.localPosition = new Vector3(
                        x * board.CellSize,
                        -tileHeight * 0.5f,
                        z * board.CellSize);

                    if (floorMaterial != null) {
                        tile.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                    }
                }
            }
        }
    }
}
