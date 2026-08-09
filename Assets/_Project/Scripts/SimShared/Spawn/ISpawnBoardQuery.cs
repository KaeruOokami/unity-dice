namespace DiceGame.SimShared.Spawn
{
    /// <summary>
    /// Board occupancy queries for spawn slot picking.
    /// Implemented by Unity <c>DiceRegistry</c> adapters and Quantum Frame adapters.
    /// </summary>
    public interface ISpawnBoardQuery
    {
        int Width { get; }
        int Height { get; }

        bool CanPlaceBottom(int x, int y);

        bool CanPlaceTop(int x, int y);

        /// <summary>Optional region filter (Versus owner areas). Default allow all in-board cells.</summary>
        bool IsCellAllowed(int x, int y);
    }

    public readonly struct SpawnCellSlot
    {
        public int X { get; }
        public int Y { get; }
        public bool IsTop { get; }

        public SpawnCellSlot(int x, int y, bool isTop)
        {
            X = x;
            Y = y;
            IsTop = isTop;
        }
    }
}
