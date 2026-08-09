namespace DiceGame.Core
{
    using UnityEngine;

    /// <summary>Unity adapter for <see cref="DirectionExtensions.GetGridDelta"/>.</summary>
    public static class DirectionUnityExtensions
    {
        public static Vector2Int ToGridDelta(this Direction direction)
        {
            direction.GetGridDelta(out var dx, out var dy);
            return new Vector2Int(dx, dy);
        }
    }
}
