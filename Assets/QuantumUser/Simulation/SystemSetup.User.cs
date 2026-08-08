namespace Quantum
{
    using System.Collections.Generic;

    public static partial class DeterministicSystemSetup
    {
        static partial void AddSystemsUser(
            ICollection<SystemBase> systems,
            RuntimeConfig gameConfig,
            SimulationConfig simulationConfig,
            SystemsConfig systemsConfig)
        {
            // Mirrors SimTickSchedule / OnlineDualSimInputBinder.StepSimulationTick.
            // JumboSequence deferred (2x2 footprint / bridged match). Ice slide & Magnet chain deferred.
            systems.Add(new BoardBootstrapSystem());
            systems.Add(new DiceLogicalMotionSystem()); // 0 DiceLogicalMotions
            systems.Add(new PlayerActionSystem());      // 1+2 ApplyInputs / Characters
            systems.Add(new DiceSpawnSystem());         // 3 Spawn
            systems.Add(new VersusAttackSystem());      // 4 VersusAttack
            systems.Add(new DiceMatchErasureSystem());  // 6 ErasureMatch
        }
    }
}
