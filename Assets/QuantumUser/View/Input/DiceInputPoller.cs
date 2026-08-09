namespace Quantum
{
    using DiceGame.Config;
    using Photon.Deterministic;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using CoreDirection = DiceGame.Core.Direction;
    using CoreInputDirection = DiceGame.Core.InputDirection;

    /// <summary>
    /// Polls the same <see cref="PlayerInputSettings"/> / InputActions as production
    /// <c>CharacterInputReader</c> into Quantum <see cref="Input"/> (J jump, K lift).
    /// </summary>
    public sealed class DiceInputPoller : MonoBehaviour
    {
        [SerializeField] PlayerInputSettings inputSettings;
        [SerializeField] PlayerSlot playerSlot = PlayerSlot.Player1;

        InputActionMap playerMap;
        InputAction moveAction;
        InputAction liftAction;
        InputAction jumpAction;
        bool bound;

        void OnEnable()
        {
            BindActions();
            playerMap?.Enable();
            QuantumCallback.Subscribe(this, (CallbackPollInput callback) => PollInput(callback));
        }

        void OnDisable()
        {
            playerMap?.Disable();
        }

        void BindActions()
        {
            bound = false;
            if (inputSettings == null)
            {
                var bootstrap = Object.FindAnyObjectByType<DiceGame.Gameplay.GameBootstrap>();
                inputSettings = bootstrap != null ? bootstrap.PlayerInputSettings : null;
            }

            if (inputSettings == null)
            {
                Debug.LogError(
                    "DiceInputPoller: assign PlayerInputSettings (same asset as CharacterInputReader).");
                return;
            }

            if (inputSettings.InputActions == null)
            {
                Debug.LogError("DiceInputPoller: PlayerInputSettings.InputActions is null.");
                return;
            }

            var mapName = inputSettings.GetActionMapName(playerSlot);
            playerMap = inputSettings.InputActions.FindActionMap(mapName, throwIfNotFound: false);
            if (playerMap == null)
            {
                Debug.LogError($"DiceInputPoller: action map '{mapName}' not found.");
                return;
            }

            moveAction = playerMap.FindAction("Move", throwIfNotFound: false);
            liftAction = playerMap.FindAction("Lift", throwIfNotFound: false);
            jumpAction = playerMap.FindAction("Jump", throwIfNotFound: false);
            if (moveAction == null || liftAction == null || jumpAction == null)
            {
                Debug.LogError("DiceInputPoller: Move/Lift/Jump actions missing on action map.");
                return;
            }

            var slotConfig = inputSettings.GetSlotConfig(playerSlot);
            playerMap.bindingMask = InputBinding.MaskByGroup(PlayerInputSettings.GetControlScheme(slotConfig));
            ApplyDeviceFilter(slotConfig);
            bound = true;
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
                    playerMap.devices = null;
                    return;
                }

                playerMap.devices = new InputDevice[] { gamepads[slotConfig.GamepadIndex] };
                return;
            }

            var keyboard = Keyboard.current;
            playerMap.devices = keyboard != null ? new InputDevice[] { keyboard } : null;
        }

        void PollInput(CallbackPollInput callback)
        {
            var input = new Input();
            if (!bound)
            {
                callback.SetInput(input, DeterministicInputFlags.Repeatable);
                return;
            }

            var axis = moveAction.ReadValue<Vector2>();
            if (CoreInputDirection.TryFromVector2(axis, out var direction))
            {
                switch (direction)
                {
                    case CoreDirection.North:
                        input.MoveN = true;
                        break;
                    case CoreDirection.South:
                        input.MoveS = true;
                        break;
                    case CoreDirection.East:
                        input.MoveE = true;
                        break;
                    case CoreDirection.West:
                        input.MoveW = true;
                        break;
                }
            }

            input.Lift = liftAction.IsPressed();
            input.Jump = jumpAction.IsPressed();
            callback.SetInput(input, DeterministicInputFlags.Repeatable);
        }
    }
}
