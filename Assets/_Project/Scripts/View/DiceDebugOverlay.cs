using UnityEngine;

namespace DiceGame.View
{
    public class DiceDebugOverlay : MonoBehaviour
    {
        DiceGame.Gameplay.CharacterController characterController;

        void Update() {
            if (characterController == null) {
                characterController = FindObjectOfType<DiceGame.Gameplay.CharacterController>();
            }
        }

        void OnGUI() {
            if (characterController == null || characterController.CurrentDice == null) {
                return;
            }

            var state = characterController.CurrentDice.CurrentState;
            GUI.Label(new Rect(12f, 12f, 400f, 24f),
                $"Top: {state.Orientation.Top}  Grid: ({state.GridPos.x}, {state.GridPos.y})  Face: {characterController.FacePosition}");
        }
    }
}
