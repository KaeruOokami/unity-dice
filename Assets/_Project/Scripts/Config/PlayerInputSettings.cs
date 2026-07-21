using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceGame.Config
{
    public enum PlayerSlot
    {
        Player1,
        Player2
    }

    public enum PlayerInputDeviceKind
    {
        Keyboard,
        Gamepad
    }

    [Serializable]
    public struct PlayerSlotInputConfig
    {
        [SerializeField] PlayerInputDeviceKind deviceKind;
        [SerializeField] int gamepadIndex;

        public PlayerSlotInputConfig(PlayerInputDeviceKind kind, int padIndex)
        {
            deviceKind = kind;
            gamepadIndex = padIndex;
        }

        public PlayerInputDeviceKind DeviceKind => deviceKind;
        public int GamepadIndex => gamepadIndex;
    }

    [CreateAssetMenu(fileName = "PlayerInputSettings", menuName = "Dice/Player Input Settings")]
    public sealed class PlayerInputSettings : ScriptableObject
    {
        public const string KeyboardScheme = "Keyboard";
        public const string GamepadScheme = "Gamepad";
        public const string Player1ActionMap = "Player1";
        public const string Player2ActionMap = "Player2";
        public const string GameFlowActionMap = "GameFlow";
        public const string PauseAction = "Pause";
        public const string ResetAction = "Reset";

        [SerializeField] InputActionAsset inputActions;

        [Header("Control Defaults")]
        [SerializeField] PlayerSlotControlDefaults player1 =
            PlayerSlotControlDefaults.Create(false, PlayerInputDeviceKind.Keyboard, 0);
        [SerializeField] PlayerSlotControlDefaults player2 =
            PlayerSlotControlDefaults.Create(true, PlayerInputDeviceKind.Gamepad, 0);

        public InputActionAsset InputActions => inputActions;
        public PlayerSlotInputConfig Player1 => player1.InputConfig;
        public PlayerSlotInputConfig Player2 => player2.InputConfig;

        public PlayerSlotControlDefaults GetControlDefaults(PlayerSlot slot)
        {
            return slot == PlayerSlot.Player1 ? player1 : player2;
        }

        public PlayerSlotInputConfig GetSlotConfig(PlayerSlot slot)
        {
            return GetControlDefaults(slot).InputConfig;
        }

        public string GetActionMapName(PlayerSlot slot)
        {
            return slot == PlayerSlot.Player1 ? Player1ActionMap : Player2ActionMap;
        }

        public static string GetControlScheme(PlayerSlotInputConfig config)
        {
            return config.DeviceKind == PlayerInputDeviceKind.Keyboard ? KeyboardScheme : GamepadScheme;
        }

        public bool TryValidateStartup(
            int requiredPlayerCount,
            bool player1IsAi,
            bool player2IsAi,
            PlayerSlotInputConfig player1Config,
            PlayerSlotInputConfig player2Config,
            out string errorMessage)
        {
            if (inputActions == null)
            {
                errorMessage = "PlayerInputSettings: InputActionAsset is not assigned.";
                return false;
            }

            if (requiredPlayerCount >= 1
                && !player1IsAi
                && !ValidateSlot(PlayerSlot.Player1, player1Config, out errorMessage))
            {
                return false;
            }

            if (requiredPlayerCount >= 2
                && !player2IsAi
                && !ValidateSlot(PlayerSlot.Player2, player2Config, out errorMessage))
            {
                return false;
            }

            if (requiredPlayerCount >= 2
                && !player1IsAi
                && !player2IsAi
                && player1Config.DeviceKind == PlayerInputDeviceKind.Gamepad
                && player2Config.DeviceKind == PlayerInputDeviceKind.Gamepad
                && player1Config.GamepadIndex == player2Config.GamepadIndex)
            {
                errorMessage = "PlayerInputSettings: Player 1 and Player 2 cannot share the same gamepad index.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        static bool ValidateSlot(PlayerSlot slot, PlayerSlotInputConfig config, out string errorMessage)
        {
            if (config.DeviceKind != PlayerInputDeviceKind.Gamepad)
            {
                errorMessage = null;
                return true;
            }

            var requiredCount = config.GamepadIndex + 1;
            if (Gamepad.all.Count < requiredCount)
            {
                errorMessage =
                    $"PlayerInputSettings: {slot} requires gamepad index {config.GamepadIndex}, but only {Gamepad.all.Count} gamepad(s) are connected.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
