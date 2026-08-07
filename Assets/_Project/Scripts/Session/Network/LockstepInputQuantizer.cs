using UnityEngine;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Makes stick/keyboard move deterministic across peers before lockstep commit.
    /// Continuum floats are snapped to an 8-direction unit vector (or zero).
    /// </summary>
    public static class LockstepInputQuantizer
    {
        public const float DeadZone = 0.2f;

        public static Vector2 QuantizeMove(Vector2 move) {
            if (move.sqrMagnitude < DeadZone * DeadZone) {
                return Vector2.zero;
            }

            var angle = Mathf.Atan2(move.y, move.x);
            var sector = Mathf.RoundToInt(angle / (Mathf.PI * 0.25f));
            var snapped = sector * (Mathf.PI * 0.25f);
            return new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped));
        }
    }
}
