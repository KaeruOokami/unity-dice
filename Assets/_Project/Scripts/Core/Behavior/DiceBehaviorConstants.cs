namespace DiceGame.Core
{
    public static class DiceBehaviorConstants
    {
        public const float StoneRollDurationMultiplier = 2f;
        public const float DefaultRollDurationMultiplier = 1f;
        public const float DefaultSpawnGravityScale = 1f;
        /// <summary>Ghost-only slower spawn fall (Iron/Stone stay at default gravity).</summary>
        public const float GhostSpawnGravityScale = 0.35f;
    }
}
