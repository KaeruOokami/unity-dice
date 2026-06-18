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
        public static Direction Opposite(this Direction direction) {
            return direction switch {
                Direction.East => Direction.West,
                Direction.West => Direction.East,
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                _ => direction
            };
        }

        public static UnityEngine.Vector2Int ToGridDelta(this Direction direction) {
            return direction switch {
                Direction.East => UnityEngine.Vector2Int.right,
                Direction.West => UnityEngine.Vector2Int.left,
                Direction.North => new UnityEngine.Vector2Int(0, 1),
                Direction.South => new UnityEngine.Vector2Int(0, -1),
                _ => UnityEngine.Vector2Int.zero
            };
        }
    }
}
