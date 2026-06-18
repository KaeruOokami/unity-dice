using DiceGame.Grid;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] GameObject diceEntityPrefab;
        [SerializeField] Vector2Int diceStartPos = new(2, 2);
        [SerializeField] DiceOrientation diceStartOrientation = DiceOrientation.Default;

        void Start() {
            if (board == null) {
                Debug.LogError("GameBootstrap: Board is not assigned.");
                return;
            }

            if (diceEntityPrefab == null) {
                Debug.LogError("GameBootstrap: DiceEntity prefab is not assigned.");
                return;
            }

            var diceEntity = Instantiate(diceEntityPrefab, transform);
            diceEntity.name = "DiceEntity";

            var diceView = diceEntity.GetComponent<DiceView>();
            if (diceView == null) {
                Debug.LogError("GameBootstrap: DiceEntity prefab must have DiceView.");
                Destroy(diceEntity);
                return;
            }

            var diceController = diceEntity.GetComponent<DiceController>();
            if (diceController == null) {
                Debug.LogError("GameBootstrap: DiceEntity prefab must have DiceController.");
                Destroy(diceEntity);
                return;
            }

            var characterController = diceEntity.GetComponent<CharacterController>();
            if (characterController == null) {
                Debug.LogError("GameBootstrap: DiceEntity prefab must have CharacterController.");
                Destroy(diceEntity);
                return;
            }

            diceController.Configure(board, diceView, diceStartPos, diceStartOrientation);
            characterController.Configure(board, diceController, diceView);
            SetupCamera(board);
        }

        static void SetupCamera(Board board) {
            var camera = Camera.main;
            if (camera == null) {
                return;
            }

            var center = board.GridToWorld(new Vector2Int(board.Width / 2, board.Height / 2));
            var distance = board.CellSize * Mathf.Max(board.Width, board.Height);
            camera.transform.position = center + new Vector3(-distance * 0.6f, distance * 0.75f, -distance * 0.6f);
            camera.transform.LookAt(center);
        }
    }
}
