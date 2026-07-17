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
        /// <summary>While jumping, block transfers onto a different die.</summary>
        public bool BlocksJumpTransferToOtherDice { get; }
        /// <summary>While jumping, forbid climbing to a higher stack tier.</summary>
        public bool BlocksJumpUpwardTierChange { get; }
        /// <summary>
        /// Max orthogonal cells for jump grid moves. 0 = use
        /// <see cref="DiceGridRollLimits.MaxParallelRollDistance"/>.
        /// </summary>
        public int MaxJumpGridMoveDistance { get; }
        /// <summary>Grid moves keep orientation (no roll faces).</summary>
        public bool PreservesOrientationOnGridMove { get; }
        /// <summary>Demote uses slide visual instead of roll visual.</summary>
        public bool UsesSlideVisualForDemote { get; }
        /// <summary>Adjacent magnet coupling is blocked while this die is present.</summary>
        public bool BlocksAdjacentMagnet { get; }

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
            bool allowsDiceSwapThrough = false,
            bool blocksJumpTransferToOtherDice = false,
            bool blocksJumpUpwardTierChange = false,
            int maxJumpGridMoveDistance = 0,
            bool preservesOrientationOnGridMove = false,
            bool usesSlideVisualForDemote = false,
            bool blocksAdjacentMagnet = false) {
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
            BlocksJumpTransferToOtherDice = blocksJumpTransferToOtherDice;
            BlocksJumpUpwardTierChange = blocksJumpUpwardTierChange;
            MaxJumpGridMoveDistance = maxJumpGridMoveDistance;
            PreservesOrientationOnGridMove = preservesOrientationOnGridMove;
            UsesSlideVisualForDemote = usesSlideVisualForDemote;
            BlocksAdjacentMagnet = blocksAdjacentMagnet;
        }

        public int GetEffectiveMaxJumpGridMoveDistance() {
            return MaxJumpGridMoveDistance > 0
                ? MaxJumpGridMoveDistance
                : DiceGridRollLimits.MaxParallelRollDistance;
        }
    }
}
