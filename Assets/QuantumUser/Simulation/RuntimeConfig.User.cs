namespace Quantum
{
    using System;
    using Photon.Deterministic;

    public partial class RuntimeConfig
    {
        /// <summary>Phase B board width in cells. Non-positive falls back to <see cref="PhaseBSimDefaults.BoardWidth"/>.</summary>
        public Int32 PhaseBBoardWidth = PhaseBSimDefaults.BoardWidth;

        /// <summary>Phase B board height in cells. Non-positive falls back to <see cref="PhaseBSimDefaults.BoardHeight"/>.</summary>
        public Int32 PhaseBBoardHeight = PhaseBSimDefaults.BoardHeight;

        /// <summary>How many dice the Phase B bootstrap spawns. Non-positive falls back to <see cref="PhaseBSimDefaults.InitialDiceCount"/>.</summary>
        public Int32 PhaseBInitialDiceCount = PhaseBSimDefaults.InitialDiceCount;

        /// <summary>World units per grid cell for Transform2D / view sync.</summary>
        public FP PhaseBCellSize = FP._1;
    }
}
