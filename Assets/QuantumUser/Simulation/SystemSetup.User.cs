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
            // Phase B vertical slice (local QuantumGameScene). Keep UGS dual-sim on the main game path.
            systems.Add(new PhaseBBootstrapSystem());
            systems.Add(new PhaseBPlayerActionSystem());
        }
    }
}
