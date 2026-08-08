namespace Quantum
{
    /// <summary>
    /// Tick-rate constants for Quantum match systems (60 Hz lockstep-aligned).
    /// </summary>
    public static class MatchSimDefaults
    {
        public const int SimTickHz = 60;

        // Erasure: SinkDuration 0.8s / RadianceDuration 0.4s
        public const int SinkEraseTicks = 48;
        public const int RadianceEraseTicks = 24;

        // Spawn: interval 2s ﾂｱ 0.5s
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
    }
}
