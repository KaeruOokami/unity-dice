using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.View
{
    public class DiceDebugOverlay : MonoBehaviour
    {
        DiceController diceController;

        void Update() {
            if (diceController == null) {
                diceController = FindObjectOfType<DiceController>();
            }
        }

        void OnGUI() {
            if (diceController == null) {
                return;
            }

            var state = diceController.CurrentState;
            GUI.Label(new Rect(12f, 12f, 320f, 24f), $"Top: {state.Orientation.Top}  Grid: ({state.GridPos.x}, {state.GridPos.y})");
        }
    }
}
