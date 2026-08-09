namespace Quantum
{
    /// <summary>
    /// Tick-rate constants for Quantum match systems (60 Hz lockstep-aligned).
    /// </summary>
    public static class MatchSimDefaults
    {
        // Keep in sync with DiceGame.Config.SimTiming.TickHz (SO stores ticks at this rate).
        public const int SimTickHz = 60;

        // Fallbacks when SO / snapshot omitted (DiceErasureSettings / DiceSpawnSettings defaults).
        public const int SinkEraseTicks = 48;
        public const int RadianceEraseTicks = 24;

        public const int SpawnIntervalTicks = 120;
        public const int SpawnJitterTicks = 30;
        public const int BottomSpawnWeightPermille = 500;

        // Versus: QueueToBoardDelay 1.5s; MVP attack volume
        public const int AttackQueueDelayTicks = 90;
        public const int AttackMaxVolley = 6;
        public const int AttackMultiplierPermille = 100; // 0.1
        public const int AttackFaceGainPermille = 400; // 0.4
        public const int AttackSizeGainPermille = 100;
        public const int AttackFaceWeightPermille = 1000;

        // Character height steps (CharacterMovementSettings defaults × 1000)
        public const int MaxWalkStepPermille = 500;
        public const int MaxJumpStepPlayerOnlyPermille = 500;
        public const int MaxJumpStepCoupledPermille = 1000;

        // Continuous move (CharacterMovementSettings defaults × 1000 for speeds)
        public const int MaxMoveSpeedMilli = 2500; // 2.5
        public const int MoveAccelerationMilli = 10000; // 10
        public const int RollTriggerExtentPermille = 350; // 0.35

        // Push / push-follow (Phase 2). ~0.35s at 60 Hz; radius ≈ capsule half-extent.
        public const int PushMotionTicks = 21;
        public const int PushContactRadiusMilli = 250; // 0.25

        // Bottom emergence default (PhysicsSettings 2.5s @ 60 Hz).
        public const int SpawnMotionTicks = 150;

        // Jump airborne fallback (~0.6s @ 60 Hz) when height/gravity omitted.
        public const int JumpAirborneTicks = 36;
        public const int JumpHeightMilli = 1000; // 1.0 world unit
        public const int JumpGravityMilli = 55000; // PhysicsSettings default 55
        // PhysicsSettings jump grid timeline defaults × 1000.
        public const int JumpGridTwoCellMaxTimelinePermille = 100;
        public const int JumpGridOneCellMaxTimelinePermille = 500;
        public const int JumpGridTierChangeMinTimelinePermille = 200;
        public const int JumpGridTierChangeMaxTimelinePermille = 500;

        public const int LiftDurationTicks = 18;
        public const int PlaceDurationTicks = 18;
        public const int SlideDurationTicks = 18;
    }
}

