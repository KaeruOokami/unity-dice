using DiceGame.Core;
using DiceGame.Gameplay.Input;
using UnityEngine;

namespace DiceGame.Session.Network
{
    [DefaultExecutionOrder(-50)]
    public sealed class RemoteNetworkInputSource : MonoBehaviour, ICharacterInputSource
    {
        Vector2 move;
        bool liftPulse;
        bool jumpPulse;
        Direction? directionPulse;

        public void ApplyPayload(OnlineInputPayload payload) {
            move = payload.Move;
            if (payload.LiftPressed) {
                liftPulse = true;
            }

            if (payload.JumpPressed) {
                jumpPulse = true;
            }

            if (payload.TryGetDirection(out var direction)) {
                directionPulse = direction;
            }
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
            liftPulse = false;
            jumpPulse = false;
            directionPulse = null;
        }
    }
}
