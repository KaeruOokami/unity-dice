using DiceGame.Config;
using DiceGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceGame.Gameplay.Input
{
    public sealed class CharacterInputReader : MonoBehaviour, ICharacterInputSource
    {
        [SerializeField] PlayerInputSettings inputSettings;

        PlayerSlot playerSlot;
        PlayerSlotInputConfig slotConfigOverride;
        bool hasSlotConfigOverride;
        InputActionMap playerMap;
        InputAction moveAction;
        InputAction liftAction;
        InputAction jumpAction;
        bool isConfigured;

        public void Configure(PlayerSlot slot, PlayerInputSettings settings)
        {
            ConfigureInternal(slot, settings, null);
        }

        public void Configure(
            PlayerSlot slot,
            PlayerInputSettings settings,
            PlayerSlotInputConfig inputOverride)
        {
            ConfigureInternal(slot, settings, inputOverride);
        }

        void ConfigureInternal(
            PlayerSlot slot,
            PlayerInputSettings settings,
            PlayerSlotInputConfig? inputOverride)
        {
            playerSlot = slot;
            inputSettings = settings;
            hasSlotConfigOverride = inputOverride.HasValue;
            if (inputOverride.HasValue) {
                slotConfigOverride = inputOverride.Value;
            }

            BindActions();
            ApplySlotConfiguration();
            isConfigured = true;

            if (isActiveAndEnabled)
            {
                playerMap?.Enable();
            }
        }

        void OnEnable()
        {
            if (isConfigured)
            {
                playerMap?.Enable();
            }
        }

        void OnDisable()
        {
            playerMap?.Disable();
        }

        void BindActions()
        {
            if (inputSettings?.InputActions == null)
            {
                return;
            }

            var mapName = inputSettings.GetActionMapName(playerSlot);
            playerMap = inputSettings.InputActions.FindActionMap(mapName, throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            liftAction = playerMap.FindAction("Lift", throwIfNotFound: true);
            jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
        }

        void ApplySlotConfiguration()
        {
            if (playerMap == null || inputSettings == null)
            {
                return;
            }

            var slotConfig = hasSlotConfigOverride
                ? slotConfigOverride
                : inputSettings.GetSlotConfig(playerSlot);
            playerMap.bindingMask = InputBinding.MaskByGroup(PlayerInputSettings.GetControlScheme(slotConfig));
            ApplyDeviceFilter(slotConfig);
        }

        void ApplyDeviceFilter(PlayerSlotInputConfig slotConfig)
        {
            if (playerMap == null)
            {
                return;
            }

            if (slotConfig.DeviceKind == PlayerInputDeviceKind.Gamepad)
            {
                var gamepads = Gamepad.all;
                if (slotConfig.GamepadIndex >= gamepads.Count)
                {
                    Debug.LogError(
                        $"CharacterInputReader: {playerSlot} gamepad index {slotConfig.GamepadIndex} is not available.");
                    playerMap.devices = null;
                    return;
                }

                playerMap.devices = new InputDevice[] { gamepads[slotConfig.GamepadIndex] };
                return;
            }

            var keyboard = Keyboard.current;
            playerMap.devices = keyboard != null ? new InputDevice[] { keyboard } : null;
        }

        public Vector2 ReadMove()
        {
            return moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        public bool WasLiftPressedThisFrame()
        {
            return liftAction != null && liftAction.WasPressedThisFrame();
        }

        public bool WasJumpPressedThisFrame()
        {
            return jumpAction != null && jumpAction.WasPressedThisFrame();
        }

        public bool TryGetDirectionPressedThisFrame(out Direction direction)
        {
            direction = default;
            if (moveAction == null || !moveAction.WasPressedThisFrame())
            {
                return false;
            }

            return TryInputToDirection(ReadMove(), out direction);
        }

        static bool TryInputToDirection(Vector2 input, out Direction direction)
        {
            direction = default;
            if (input.sqrMagnitude <= 0f)
            {
                return false;
            }

            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
            {
                direction = input.x > 0f ? Direction.East : Direction.West;
            }
            else
            {
                direction = input.y > 0f ? Direction.North : Direction.South;
            }

            return true;
        }
    }
}
