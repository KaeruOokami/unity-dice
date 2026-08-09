namespace DiceGame.Core
{
    public enum Direction
    {
        East,
        West,
        North,
        South
    }

    public static class DirectionExtensions
    {
        public static Direction Opposite(this Direction direction)
        {
            return direction switch
            {
                Direction.East => Direction.West,
                Direction.West => Direction.East,
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                _ => direction
            };
        }

        /// <summary>Grid delta without Unity types (Quantum / Domain safe).</summary>
        public static void GetGridDelta(this Direction direction, out int dx, out int dy)
        {
            switch (direction)
            {
                case Direction.East:
                    dx = 1;
                    dy = 0;
                    return;
                case Direction.West:
                    dx = -1;
                    dy = 0;
                    return;
                case Direction.North:
                    dx = 0;
                    dy = 1;
                    return;
                case Direction.South:
                    dx = 0;
                    dy = -1;
                    return;
                default:
                    dx = 0;
                    dy = 0;
                    return;
            }
        }
    }
}
