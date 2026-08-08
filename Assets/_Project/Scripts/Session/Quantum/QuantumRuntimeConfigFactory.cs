namespace DiceGame.Session
{
    using DiceGame.Config;
    using Photon.Deterministic;
    using Quantum;

    /// <summary>
    /// Builds <see cref="RuntimeConfig"/> for Quantum sessions from match setup + seed.
    /// </summary>
    public static class QuantumRuntimeConfigFactory
    {
        public static RuntimeConfig Create(
            int seed,
            int boardWidth,
            int boardHeight,
            int initialDiceCount)
        {
            var config = new RuntimeConfig
            {
                Seed = seed,
                BoardWidth = boardWidth > 0 ? boardWidth : BoardDefaults.BoardWidth,
                BoardHeight = boardHeight > 0 ? boardHeight : BoardDefaults.BoardHeight,
                InitialDiceCount = initialDiceCount > 0 ? initialDiceCount : BoardDefaults.InitialDiceCount,
                CellSize = FP._1,
                SinkEraseTicks = MatchSimDefaults.SinkEraseTicks,
                RadianceEraseTicks = MatchSimDefaults.RadianceEraseTicks,
                SpawnIntervalTicks = MatchSimDefaults.SpawnIntervalTicks,
                SpawnJitterTicks = MatchSimDefaults.SpawnJitterTicks,
                BottomSpawnWeightPermille = MatchSimDefaults.BottomSpawnWeightPermille,
                AttackQueueDelayTicks = MatchSimDefaults.AttackQueueDelayTicks,
                AttackMaxVolley = MatchSimDefaults.AttackMaxVolley,
                AttackMultiplierPermille = MatchSimDefaults.AttackMultiplierPermille,
                AttackFaceGainPermille = MatchSimDefaults.AttackFaceGainPermille,
                AttackSizeGainPermille = MatchSimDefaults.AttackSizeGainPermille,
            };

            if (QuantumDefaultConfigs.TryGetGlobal(out var defaults))
            {
                config.SimulationConfig = defaults.SimulationConfig;
                config.SystemsConfig = defaults.SystemsConfig;
            }

            return config;
        }

        public static RuntimeConfig CreateFromSetup(int seed, MatchSetupSnapshot snapshot)
        {
            var initialDice = BoardDefaults.InitialDiceCount;
            if (snapshot != null)
            {
                if (snapshot.GameMode == GameMode.Versus)
                {
                    initialDice = snapshot.GetVersusSharedInitialDiceCount();
                }
                else
                {
                    initialDice = UnityEngine.Mathf.Max(1, snapshot.Player1.Spawn.InitialDiceCount);
                }
            }

            return Create(
                seed,
                BoardDefaults.BoardWidth,
                BoardDefaults.BoardHeight,
                initialDice);
        }
    }
}
