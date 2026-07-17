namespace DiceGame.Core
{
    public readonly struct DiceCapabilities
    {
        public bool CanBePushedByPlayer { get; }
        public bool CanBeLiftedByPlayer { get; }
        public bool CanJumpCoupleWithPlayer { get; }
        public bool PushUsesRoll { get; }
        public bool CanGridRoll { get; }
        public bool SlideUntilBlocked { get; }
        public bool HasMagnetCoupling { get; }
        public bool HasSpawnBounce { get; }
        public float SpawnGravityScale { get; }
        public float RollDurationMultiplier { get; }
        /// <summary>Player walks through; cannot stand on or ride this die.</summary>
        public bool IsPlayerPassThrough { get; }
        /// <summary>
        /// Other dice can swap into this die's cell (horizontal) or force an in-cell promote
        /// when trying to stack on top of it.
        /// </summary>
        public bool AllowsDiceSwapThrough { get; }

        public DiceCapabilities(
            bool canBePushedByPlayer,
            bool canBeLiftedByPlayer,
            bool canJumpCoupleWithPlayer,
            bool pushUsesRoll,
            bool canGridRoll,
            bool slideUntilBlocked,
            bool hasMagnetCoupling,
            bool hasSpawnBounce,
            float rollDurationMultiplier,
            float spawnGravityScale = DiceBehaviorConstants.DefaultSpawnGravityScale,
            bool isPlayerPassThrough = false,
            bool allowsDiceSwapThrough = false) {
            CanBePushedByPlayer = canBePushedByPlayer;
            CanBeLiftedByPlayer = canBeLiftedByPlayer;
            CanJumpCoupleWithPlayer = canJumpCoupleWithPlayer;
            PushUsesRoll = pushUsesRoll;
            CanGridRoll = canGridRoll;
            SlideUntilBlocked = slideUntilBlocked;
            HasMagnetCoupling = hasMagnetCoupling;
            HasSpawnBounce = hasSpawnBounce;
            SpawnGravityScale = spawnGravityScale;
            RollDurationMultiplier = rollDurationMultiplier;
            IsPlayerPassThrough = isPlayerPassThrough;
            AllowsDiceSwapThrough = allowsDiceSwapThrough;
        }
    }
}
