using DiceGame.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceGame.Gameplay.Input
{
    public sealed class GameFlowInputReader : MonoBehaviour
    {
        InputActionMap gameFlowMap;
        InputAction pauseAction;
        InputAction resetAction;
        InputActionMap[] gameplayMaps;
        bool isConfigured;

        public void Configure(PlayerInputSettings inputSettings, int requiredPlayerCount)
        {
            if (inputSettings?.InputActions == null)
            {
                Debug.LogError("GameFlowInputReader: InputActionAsset is not assigned.");
                return;
            }

            gameFlowMap = inputSettings.InputActions.FindActionMap(
                PlayerInputSettings.GameFlowActionMap,
                throwIfNotFound: true);
            pauseAction = gameFlowMap.FindAction(
                PlayerInputSettings.PauseAction,
                throwIfNotFound: true);
            resetAction = gameFlowMap.FindAction(
                PlayerInputSettings.ResetAction,
                throwIfNotFound: true);

            gameplayMaps = new InputActionMap[requiredPlayerCount];
            for (var i = 0; i < gameplayMaps.Length; i++)
            {
                var slot = i == 0 ? PlayerSlot.Player1 : PlayerSlot.Player2;
                gameplayMaps[i] = inputSettings.InputActions.FindActionMap(
                    inputSettings.GetActionMapName(slot),
                    throwIfNotFound: true);
            }

            isConfigured = true;
            if (isActiveAndEnabled)
            {
                gameFlowMap.Enable();
            }
        }

        void OnEnable()
        {
            if (isConfigured)
            {
                gameFlowMap?.Enable();
            }
        }

        void OnDisable()
        {
            gameFlowMap?.Disable();
        }

        public bool WasPausePressedThisFrame()
        {
            return pauseAction != null && pauseAction.WasPressedThisFrame();
        }

        public bool WasResetPressedThisFrame()
        {
            return resetAction != null && resetAction.WasPressedThisFrame();
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            if (gameplayMaps == null)
            {
                return;
            }

            for (var i = 0; i < gameplayMaps.Length; i++)
            {
                if (enabled)
                {
                    gameplayMaps[i]?.Enable();
                }
                else
                {
                    gameplayMaps[i]?.Disable();
                }
            }
        }
    }
}
