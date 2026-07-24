using DiceGame.Core;
using DiceGame.Gameplay.Input;
using UnityEngine;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Holds the input for the current lockstep tick (pulses are per-tick, not per render frame).
    /// </summary>
    public sealed class LockstepTickInputSource : MonoBehaviour, ICharacterInputSource
    {
        OnlineInputPayload current;

        public void SetTickInput(OnlineInputPayload payload) {
            current = payload;
        }

        public Vector2 ReadMove() {
            return current.Move;
        }

        public bool WasLiftPressedThisFrame() {
            return current.LiftPressed;
        }

        public bool WasJumpPressedThisFrame() {
            return current.JumpPressed;
        }

        public bool TryGetDirectionPressedThisFrame(out Direction direction) {
            return current.TryGetDirection(out direction);
        }
    }
}
