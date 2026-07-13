using DiceGame.Core;
using DiceGame.Gameplay.Input;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    [DefaultExecutionOrder(-50)]
    public sealed class AiCharacterInputSource : MonoBehaviour, ICharacterInputSource
    {
        Vector2 move;
        bool liftPulse;
        bool jumpPulse;
        Direction? directionPulse;

        public void SetMove(Vector2 value) {
            move = value;
        }

        public void PulseLift() {
            liftPulse = true;
        }

        public void PulseJump() {
            jumpPulse = true;
        }

        public void PulseDirection(Direction direction) {
            directionPulse = direction;
        }

        public void ClearFrameInputs() {
            liftPulse = false;
            jumpPulse = false;
            directionPulse = null;
        }

        public Vector2 ReadMove() {
            return move;
        }

        public bool WasLiftPressedThisFrame() {
            return liftPulse;
        }

        public bool WasJumpPressedThisFrame() {
            return jumpPulse;
        }

        public bool TryGetDirectionPressedThisFrame(out Direction direction) {
            if (directionPulse.HasValue) {
                direction = directionPulse.Value;
                return true;
            }

            direction = default;
            return false;
        }

        void LateUpdate() {
            ClearFrameInputs();
        }
    }
}
