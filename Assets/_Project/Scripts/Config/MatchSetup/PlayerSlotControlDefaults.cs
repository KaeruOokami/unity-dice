using System;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct PlayerSlotControlDefaults
    {
        [SerializeField] bool isAi;
        [SerializeField] PlayerSlotInputConfig inputConfig;

        public bool IsAi => isAi;
        public PlayerSlotInputConfig InputConfig => inputConfig;

        public PlayerSlotControlDefaults(bool aiControlled, PlayerSlotInputConfig input) {
            isAi = aiControlled;
            inputConfig = input;
        }

        public static PlayerSlotControlDefaults Create(
            bool aiControlled,
            PlayerInputDeviceKind deviceKind,
            int gamepadIndex) {
            return new PlayerSlotControlDefaults(
                aiControlled,
                new PlayerSlotInputConfig(deviceKind, gamepadIndex));
        }
    }
}
