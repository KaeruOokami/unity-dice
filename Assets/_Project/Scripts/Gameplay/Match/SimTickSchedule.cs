namespace DiceGame.Gameplay
{
    /// <summary>
    /// Fixed online/offline simulation step order for lockstep and future Quantum Frame systems.
    /// Callers must not reorder these stages when advancing authoritative gameplay.
    /// </summary>
    public static class SimTickSchedule
    {
        public const int StageCount = 7;

        /// <summary>1. Advance dice logical busy / erasure timers and occupancy side effects.</summary>
        public const int DiceLogicalMotions = 0;

        /// <summary>2. Latch both players' inputs for this tick.</summary>
        public const int ApplyInputs = 1;

        /// <summary>3. Character gameplay (move / push / lift / jump).</summary>
        public const int Characters = 2;

        /// <summary>4. Continuous spawn cooldowns.</summary>
        public const int Spawn = 3;

        /// <summary>5. Versus attack / natural-send queues.</summary>
        public const int VersusAttack = 4;

        /// <summary>6. Pending jumbo spawn retries.</summary>
        public const int JumboSequence = 5;

        /// <summary>7. Flush pending tier-fall match evaluation.</summary>
        public const int ErasureMatch = 6;

        public static readonly string[] StageNames = {
            nameof(DiceLogicalMotions),
            nameof(ApplyInputs),
            nameof(Characters),
            nameof(Spawn),
            nameof(VersusAttack),
            nameof(JumboSequence),
            nameof(ErasureMatch)
        };
    }
}
