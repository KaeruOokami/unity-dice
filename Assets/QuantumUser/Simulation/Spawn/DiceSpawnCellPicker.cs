namespace Quantum
{
    using DiceGame.SimShared.Spawn;

    /// <summary>
    /// Quantum adapter over shared <see cref="SpawnSlotPicker"/> (same algorithm as Unity Placement).
    /// </summary>
    public static unsafe class DiceSpawnCellPicker
    {
        public static bool TryPickRandomSpawnSlot(
            Frame frame,
            Board board,
            int bottomWeightPermille,
            out int x,
            out int y,
            out DiceStackTier tier)
        {
            x = 0;
            y = 0;
            tier = DiceStackTier.Bottom;

            var weight = bottomWeightPermille / 1000f;
            var query = new FrameSpawnBoardQuery(frame, board);
            if (!SpawnSlotPicker.TryPickRandomSlot(
                    query,
                    weight,
                    (min, maxExclusive) => frame.RNG->Next(min, maxExclusive),
                    out var slot))
            {
                return false;
            }

            x = slot.X;
            y = slot.Y;
            tier = slot.IsTop ? DiceStackTier.Top : DiceStackTier.Bottom;
            return true;
        }

        public static bool TryPickAttackSpawnSlot(
            Frame frame,
            Board board,
            out int x,
            out int y,
            out DiceStackTier tier)
        {
            x = 0;
            y = 0;
            tier = default;

            var query = new FrameSpawnBoardQuery(frame, board);
            if (!SpawnSlotPicker.TryPickAttackSlot(query, out var slot))
            {
                return false;
            }

            x = slot.X;
            y = slot.Y;
            tier = slot.IsTop ? DiceStackTier.Top : DiceStackTier.Bottom;
            return true;
        }

        sealed class FrameSpawnBoardQuery : ISpawnBoardQuery
        {
            readonly Frame frame;
            readonly Board board;

            public FrameSpawnBoardQuery(Frame frame, Board board)
            {
                this.frame = frame;
                this.board = board;
            }

            public int Width => board.Width;
            public int Height => board.Height;

            public bool IsCellAllowed(int x, int y) => true;

            public bool CanPlaceBottom(int x, int y)
            {
                return CellOccupancy.CanPlaceBottomAt(frame, board, x, y);
            }

            public bool CanPlaceTop(int x, int y)
            {
                return CellOccupancy.CanPlaceTopAt(frame, board, x, y);
            }
        }
    }
}
