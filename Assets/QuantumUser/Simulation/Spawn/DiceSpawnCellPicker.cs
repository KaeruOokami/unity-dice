namespace Quantum
{
    /// <summary>
    /// Quantum port of <c>DiceSpawnCellPicker</c> (random continuous + sequential attack edge fill).
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

            var bottomCandidates = stackalloc int[64];
            var topCandidates = stackalloc int[64];
            var bottomCount = 0;
            var topCount = 0;

            for (var cy = 0; cy < board.Height; cy++)
            {
                for (var cx = 0; cx < board.Width; cx++)
                {
                    if (CellOccupancy.CanPlaceBottomAt(frame, board, cx, cy) && bottomCount < 64)
                    {
                        bottomCandidates[bottomCount++] = Encode(cx, cy, board.Width);
                    }

                    if (CellOccupancy.CanPlaceTopAt(frame, board, cx, cy) && topCount < 64)
                    {
                        topCandidates[topCount++] = Encode(cx, cy, board.Width);
                    }
                }
            }

            var wantBottom = frame.RNG->Next(0, 1000) < bottomWeightPermille;
            if (wantBottom && bottomCount > 0)
            {
                Decode(bottomCandidates[frame.RNG->Next(0, bottomCount)], board.Width, out x, out y);
                tier = DiceStackTier.Bottom;
                return true;
            }

            if (topCount > 0)
            {
                Decode(topCandidates[frame.RNG->Next(0, topCount)], board.Width, out x, out y);
                tier = DiceStackTier.Top;
                return true;
            }

            if (bottomCount > 0)
            {
                Decode(bottomCandidates[frame.RNG->Next(0, bottomCount)], board.Width, out x, out y);
                tier = DiceStackTier.Bottom;
                return true;
            }

            return false;
        }

        public static bool TryPickAttackSpawnSlot(
            Frame frame,
            Board board,
            out int x,
            out int y,
            out DiceStackTier tier)
        {
            // Edge-first refill (bottom preference) similar to TryPickSequentialAttackSpawnSlot.
            for (var cy = 0; cy < board.Height; cy++)
            {
                for (var cx = 0; cx < board.Width; cx++)
                {
                    if (CellOccupancy.CanPlaceBottomAt(frame, board, cx, cy))
                    {
                        x = cx;
                        y = cy;
                        tier = DiceStackTier.Bottom;
                        return true;
                    }
                }
            }

            for (var cy = 0; cy < board.Height; cy++)
            {
                for (var cx = 0; cx < board.Width; cx++)
                {
                    if (CellOccupancy.CanPlaceTopAt(frame, board, cx, cy))
                    {
                        x = cx;
                        y = cy;
                        tier = DiceStackTier.Top;
                        return true;
                    }
                }
            }

            x = 0;
            y = 0;
            tier = default;
            return false;
        }

        static int Encode(int x, int y, int width) => y * width + x;

        static void Decode(int packed, int width, out int x, out int y)
        {
            x = packed % width;
            y = packed / width;
        }
    }
}
