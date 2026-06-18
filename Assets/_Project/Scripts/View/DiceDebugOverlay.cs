using UnityEngine;

namespace DiceGame.View
{
    public class DiceDebugOverlay : MonoBehaviour
    {
        DiceGame.Gameplay.CharacterController characterController;
        int lastTop = -1;
        Vector2Int lastGridPos = new(-1, -1);
        Vector2 lastFacePosition;

        void Update() {
            if (characterController == null) {
                characterController = FindObjectOfType<DiceGame.Gameplay.CharacterController>();
            }

            if (characterController == null || characterController.CurrentDice == null) {
                return;
            }

            var state = characterController.CurrentDice.CurrentState;
            var top = state.Orientation.Top;
            var gridPos = state.GridPos;
            var facePosition = characterController.FacePosition;

            if (top == lastTop && gridPos == lastGridPos && facePosition == lastFacePosition) {
                return;
            }

            lastTop = top;
            lastGridPos = gridPos;
            lastFacePosition = facePosition;

            Debug.Log($"Top: {top}  Grid: ({gridPos.x}, {gridPos.y})  Face: {facePosition}");
        }
    }
}