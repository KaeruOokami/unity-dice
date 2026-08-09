namespace DiceGame.Core
{
    using UnityEngine;

    /// <summary>
    /// Shared cardinalization for move input (world axes, not camera-relative).
    /// Used by character input and Quantum input polling — do not fork this rule.
    /// </summary>
    public static class InputDirection
    {
        public static bool TryFromVector2(Vector2 input, out Direction direction)
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

        public static void ToGridDelta(Direction direction, out int dx, out int dy)
        {
            switch (direction)
            {
                case Direction.East:
                    dx = 1;
                    dy = 0;
                    break;
                case Direction.West:
                    dx = -1;
                    dy = 0;
                    break;
                case Direction.North:
                    dx = 0;
                    dy = 1;
                    break;
                case Direction.South:
                    dx = 0;
                    dy = -1;
                    break;
                default:
                    dx = 0;
                    dy = 0;
                    break;
            }
        }
    }
}
