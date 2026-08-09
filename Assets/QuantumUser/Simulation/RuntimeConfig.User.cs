namespace Quantum
{
    using System;
    using Photon.Deterministic;

    public partial class RuntimeConfig
    {
        /// <summary>Board width in cells. Non-positive falls back to <see cref="BoardDefaults.BoardWidth"/>.</summary>
        public Int32 BoardWidth = BoardDefaults.BoardWidth;

        /// <summary>Board height in cells. Non-positive falls back to <see cref="BoardDefaults.BoardHeight"/>.</summary>
        public Int32 BoardHeight = BoardDefaults.BoardHeight;

        /// <summary>Initial dice count (production <c>DiceSpawnSettings.InitialDiceCount</c>). Non-positive falls back to defaults.</summary>
        public Int32 InitialDiceCount = BoardDefaults.InitialDiceCount;

        /// <summary>Players that need a standing die (1 Single, 2 Coop/Versus).</summary>
        public Int32 RequiredPlayerCount = 1;

        /// <summary>World units per grid cell for Transform2D / view sync.</summary>
        public FP CellSize = FP._1;

        public Int32 SinkEraseTicks = MatchSimDefaults.SinkEraseTicks;
        public Int32 RadianceEraseTicks = MatchSimDefaults.RadianceEraseTicks;
        public Int32 SpawnIntervalTicks = MatchSimDefaults.SpawnIntervalTicks;
        public Int32 SpawnJitterTicks = MatchSimDefaults.SpawnJitterTicks;
        public Int32 BottomSpawnWeightPermille = MatchSimDefaults.BottomSpawnWeightPermille;
        public Int32 AttackQueueDelayTicks = MatchSimDefaults.AttackQueueDelayTicks;
        public Int32 AttackMaxVolley = MatchSimDefaults.AttackMaxVolley;
        public Int32 AttackMultiplierPermille = MatchSimDefaults.AttackMultiplierPermille;
        public Int32 AttackFaceGainPermille = MatchSimDefaults.AttackFaceGainPermille;
        public Int32 AttackSizeGainPermille = MatchSimDefaults.AttackSizeGainPermille;

        public Int32 MaxWalkStepPermille = MatchSimDefaults.MaxWalkStepPermille;
        public Int32 MaxJumpStepPlayerOnlyPermille = MatchSimDefaults.MaxJumpStepPlayerOnlyPermille;
        public Int32 MaxJumpStepCoupledPermille = MatchSimDefaults.MaxJumpStepCoupledPermille;

        public Int32 MaxMoveSpeedMilli = MatchSimDefaults.MaxMoveSpeedMilli;
        public Int32 MoveAccelerationMilli = MatchSimDefaults.MoveAccelerationMilli;
        public Int32 RollTriggerExtentPermille = MatchSimDefaults.RollTriggerExtentPermille;

        public Int32 PushMotionTicks = MatchSimDefaults.PushMotionTicks;
        public Int32 PushContactRadiusMilli = MatchSimDefaults.PushContactRadiusMilli;
        public Int32 SpawnMotionTicks = MatchSimDefaults.SpawnMotionTicks;
        public Int32 JumpAirborneTicks = MatchSimDefaults.JumpAirborneTicks;
        public Int32 JumpHeightMilli = MatchSimDefaults.JumpHeightMilli;
        public Int32 JumpHeightDiceMultiplierPermille = 1000;
        public Int32 JumpGravityMilli = MatchSimDefaults.JumpGravityMilli;
        public Int32 JumpGridTwoCellMaxTimelinePermille = MatchSimDefaults.JumpGridTwoCellMaxTimelinePermille;
        public Int32 JumpGridOneCellMaxTimelinePermille = MatchSimDefaults.JumpGridOneCellMaxTimelinePermille;
        public Int32 JumpGridTierChangeMinTimelinePermille = MatchSimDefaults.JumpGridTierChangeMinTimelinePermille;
        public Int32 JumpGridTierChangeMaxTimelinePermille = MatchSimDefaults.JumpGridTierChangeMaxTimelinePermille;
        public Int32 LiftDurationTicks = MatchSimDefaults.LiftDurationTicks;
        public Int32 PlaceDurationTicks = MatchSimDefaults.PlaceDurationTicks;
        public Int32 SlideDurationTicks = MatchSimDefaults.SlideDurationTicks;
        public Int32 BoardPartitionX = 0;
    }
}

