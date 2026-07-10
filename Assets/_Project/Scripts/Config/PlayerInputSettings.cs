using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceGame.Config
{
    public enum PlayerInputDeviceKind
    {
        Keyboard,
        Gamepad
    }

    [CreateAssetMenu(fileName = "PlayerInputSettings", menuName = "Dice/Player Input Settings")]
    public sealed class PlayerInputSettings : ScriptableObject
    {
        public const string KeyboardScheme = "Keyboard";
        public const string GamepadScheme = "Gamepad";

        [SerializeField] PlayerInputDeviceKind deviceKind = PlayerInputDeviceKind.Keyboard;
        [SerializeField] InputActionAsset inputActions;

        public PlayerInputDeviceKind DeviceKind => deviceKind;
        public InputActionAsset InputActions => inputActions;

        public string ActiveControlScheme =>
            deviceKind == PlayerInputDeviceKind.Keyboard ? KeyboardScheme : GamepadScheme;
    }
}
