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

        /// <summary>Extra random dice after the scripted seed. Non-positive falls back to defaults.</summary>
        public Int32 InitialDiceCount = BoardDefaults.InitialDiceCount;

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
    }
}
