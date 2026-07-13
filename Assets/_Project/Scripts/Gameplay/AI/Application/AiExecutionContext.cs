using DiceGame.Config;
using DiceGame.Gameplay.Input;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    public sealed class AiExecutionContext
    {
        public CharacterController Character { get; }
        public DiceRegistry Registry { get; }
        public AiCharacterInputSource InputSource { get; }
        public AiPlayerSettings Settings { get; }

        public AiExecutionContext(
            CharacterController character,
            DiceRegistry registry,
            AiCharacterInputSource inputSource,
            Config.AiPlayerSettings settings) {
            Character = character;
            Registry = registry;
            InputSource = inputSource;
            Settings = settings;
        }

        public bool IsWorldIdle() {
            if (Character == null || !Character.IsReadyForAiPlanning()) {
                return false;
            }

            if (Registry != null && Registry.AnyRolling()) {
                return false;
            }

            if (Registry != null && Registry.AnyCarried() && !Character.IsLiftCarrying) {
                return false;
            }

            return true;
        }
    }
}
