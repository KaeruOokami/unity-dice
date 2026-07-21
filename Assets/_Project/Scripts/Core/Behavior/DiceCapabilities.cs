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
        /// <summary>
        /// Multiplier on shared gravity for vertical falls (spawn appear, unsupported demote, etc.).
        /// </summary>
        public float FallGravityScale { get; }
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
        /// <summary>
        /// While jumping, forbid coupled dice grid moves (stationary / TierLanding-only).
        /// </summary>
        public bool BlocksJumpCrossCellMove { get; }
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
        /// <summary>
        /// Sliding into a stationary die with the same flag transfers slide momentum
        /// (this die stops; the other starts sliding in the same direction).
        /// </summary>
        public bool TransfersSlideOnCollision { get; }
        /// <summary>Slide / grid traversal may cross the versus partition boundary.</summary>
        public bool IgnoresPartitionBoundary { get; }
        /// <summary>Adjacent magnet coupling is blocked while this die is present.</summary>
        public bool BlocksAdjacentMagnet { get; }
        /// <summary>
        /// Covering this die (same cell, level above the player) crushes the player after settle.
        /// </summary>
        public bool CrushesPlayerOnCover { get; }
        /// <summary>Occupies a multi-cell footprint (jumbo 2x2).</summary>
        public bool HasExpandedFootprint { get; }
        /// <summary>Sink erasure never becomes an erasure ghost.</summary>
        public bool SuppressesErasureGhost { get; }
        /// <summary>Jump landing must not accelerate this die's sink.</summary>
        public bool BlocksJumpLandingSinkAdvance { get; }
        /// <summary>Multiplier on sink erasure duration (jumbo = 2).</summary>
        public float SinkDurationMultiplier { get; }
        /// <summary>
        /// While sink-erasing, match weight per tier for expanded-footprint dice.
        /// 0 = treat as 1. Jumbo uses 4 (Bottom 4 / Top 4).
        /// </summary>
        public int SinkingMatchWeightPerTier { get; }
        /// <summary>
        /// While sink-erasing, participate in both Bottom and Top match layers.
        /// </summary>
        public bool ParticipatesInBothTiersWhileSinking { get; }

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
            float fallGravityScale = DiceBehaviorConstants.DefaultFallGravityScale,
            bool isPlayerPassThrough = false,
            bool allowsDiceSwapThrough = false,
            bool blocksJumpTransferToOtherDice = false,
            bool blocksJumpCrossCellMove = false,
            bool blocksJumpUpwardTierChange = false,
            int maxJumpGridMoveDistance = 0,
            bool preservesOrientationOnGridMove = false,
            bool usesSlideVisualForDemote = false,
            bool transfersSlideOnCollision = false,
            bool ignoresPartitionBoundary = false,
            bool blocksAdjacentMagnet = false,
            bool crushesPlayerOnCover = false,
            bool hasExpandedFootprint = false,
            bool suppressesErasureGhost = false,
            bool blocksJumpLandingSinkAdvance = false,
            float sinkDurationMultiplier = 1f,
            int sinkingMatchWeightPerTier = 0,
            bool participatesInBothTiersWhileSinking = false) {
            CanBePushedByPlayer = canBePushedByPlayer;
            CanBeLiftedByPlayer = canBeLiftedByPlayer;
            CanJumpCoupleWithPlayer = canJumpCoupleWithPlayer;
            PushUsesRoll = pushUsesRoll;
            CanGridRoll = canGridRoll;
            SlideUntilBlocked = slideUntilBlocked;
            HasMagnetCoupling = hasMagnetCoupling;
            HasSpawnBounce = hasSpawnBounce;
            FallGravityScale = fallGravityScale;
            RollDurationMultiplier = rollDurationMultiplier;
            IsPlayerPassThrough = isPlayerPassThrough;
            AllowsDiceSwapThrough = allowsDiceSwapThrough;
            BlocksJumpTransferToOtherDice = blocksJumpTransferToOtherDice;
            BlocksJumpCrossCellMove = blocksJumpCrossCellMove;
            BlocksJumpUpwardTierChange = blocksJumpUpwardTierChange;
            MaxJumpGridMoveDistance = maxJumpGridMoveDistance;
            PreservesOrientationOnGridMove = preservesOrientationOnGridMove;
            UsesSlideVisualForDemote = usesSlideVisualForDemote;
            TransfersSlideOnCollision = transfersSlideOnCollision;
            IgnoresPartitionBoundary = ignoresPartitionBoundary;
            BlocksAdjacentMagnet = blocksAdjacentMagnet;
            CrushesPlayerOnCover = crushesPlayerOnCover;
            HasExpandedFootprint = hasExpandedFootprint;
            SuppressesErasureGhost = suppressesErasureGhost;
            BlocksJumpLandingSinkAdvance = blocksJumpLandingSinkAdvance;
            SinkDurationMultiplier = sinkDurationMultiplier <= 0f ? 1f : sinkDurationMultiplier;
            SinkingMatchWeightPerTier = sinkingMatchWeightPerTier;
            ParticipatesInBothTiersWhileSinking = participatesInBothTiersWhileSinking;
        }

        public int GetEffectiveMaxJumpGridMoveDistance() {
            return MaxJumpGridMoveDistance > 0
                ? MaxJumpGridMoveDistance
                : DiceGridRollLimits.MaxParallelRollDistance;
        }
    }
}
