using DiceGame.Config;
using DiceGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceGame.Gameplay.Input
{
    public sealed class CharacterInputReader : MonoBehaviour
    {
        [SerializeField] PlayerInputSettings inputSettings;

        InputActionMap playerMap;
        InputAction moveAction;
        InputAction liftAction;
        InputAction jumpAction;
        bool isConfigured;

        public void Configure(PlayerInputSettings settings)
        {
            inputSettings = settings;
            BindActions();
            ApplyControlScheme();
            isConfigured = true;

            if (isActiveAndEnabled)
            {
                playerMap?.Enable();
            }
        }

        void Awake()
        {
            if (inputSettings != null)
            {
                BindActions();
            }
        }

        void OnEnable()
        {
            if (!isConfigured && inputSettings != null)
            {
                BindActions();
                ApplyControlScheme();
                isConfigured = true;
            }

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

            playerMap = inputSettings.InputActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            liftAction = playerMap.FindAction("Lift", throwIfNotFound: true);
            jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
        }

        void ApplyControlScheme()
        {
            if (playerMap == null || inputSettings == null)
            {
                return;
            }

            playerMap.bindingMask = InputBinding.MaskByGroup(inputSettings.ActiveControlScheme);
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
