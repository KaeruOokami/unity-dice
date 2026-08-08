namespace Quantum
{
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Polls local Phase B input into Quantum <see cref="Input"/> buttons.
    /// Replaces <see cref="QuantumDebugInput"/> on <c>QuantumGameScene</c>.
    /// Main UGS dual-sim path is unchanged.
    /// </summary>
    public sealed class PhaseBInputPoller : MonoBehaviour
    {
        [SerializeField] Key moveNorth = Key.W;
        [SerializeField] Key moveSouth = Key.S;
        [SerializeField] Key moveEast = Key.D;
        [SerializeField] Key moveWest = Key.A;
        [SerializeField] Key moveNorthAlt = Key.UpArrow;
        [SerializeField] Key moveSouthAlt = Key.DownArrow;
        [SerializeField] Key moveEastAlt = Key.RightArrow;
        [SerializeField] Key moveWestAlt = Key.LeftArrow;
        [SerializeField] Key lift = Key.Space;
        [SerializeField] Key jump = Key.LeftShift;
        [SerializeField] float stickDeadZone = 0.2f;

        void OnEnable()
        {
            QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
        }

        void PollInput(CallbackPollInput callback)
        {
            var input = new Input();
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            var stick = Vector2.zero;
            if (gamepad != null)
            {
                stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude < stickDeadZone * stickDeadZone)
                {
                    stick = Vector2.zero;
                }
            }

            var north = IsPressed(keyboard, moveNorth) || IsPressed(keyboard, moveNorthAlt) || stick.y > stickDeadZone;
            var south = IsPressed(keyboard, moveSouth) || IsPressed(keyboard, moveSouthAlt) || stick.y < -stickDeadZone;
            var east = IsPressed(keyboard, moveEast) || IsPressed(keyboard, moveEastAlt) || stick.x > stickDeadZone;
            var west = IsPressed(keyboard, moveWest) || IsPressed(keyboard, moveWestAlt) || stick.x < -stickDeadZone;

            // Cardinal priority matches Phase A lockstep quantization spirit (one direction per tick).
            if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y) && stick.sqrMagnitude > 0f)
            {
                north = false;
                south = false;
            }
            else if (stick.sqrMagnitude > 0f)
            {
                east = false;
                west = false;
            }

            input.MoveN = north;
            input.MoveS = south;
            input.MoveE = east;
            input.MoveW = west;
            input.Lift = IsPressed(keyboard, lift) || (gamepad != null && gamepad.buttonSouth.isPressed);
            input.Jump = IsPressed(keyboard, jump) || (gamepad != null && gamepad.buttonNorth.isPressed);

            callback.SetInput(input, DeterministicInputFlags.Repeatable);
        }

        static bool IsPressed(Keyboard keyboard, Key key)
        {
            return keyboard != null && keyboard[key].isPressed;
        }
    }
}
