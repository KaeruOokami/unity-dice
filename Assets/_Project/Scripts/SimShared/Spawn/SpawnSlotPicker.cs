namespace DiceGame.SimShared.Spawn
{
    using System;

    /// <summary>
    /// Shared spawn slot picker (production <c>DiceSpawnCellPicker</c> algorithm).
    /// Unity and Quantum both call this — do not fork the weighted Bottom/Top pick.
    /// </summary>
    public static class SpawnSlotPicker
    {
        const int MaxCandidates = 256;

        /// <param name="next">Inclusive min, exclusive max — same contract as <c>Random.Next</c>.</param>
        public static int PickRandomSlots(
            ISpawnBoardQuery board,
            int count,
            float bottomSpawnWeight,
            Func<int, int, int> next,
            SpawnCellSlot[] results)
        {
            if (board == null || count <= 0 || results == null || next == null)
            {
                return 0;
            }

            var bottom = new int[MaxCandidates];
            var top = new int[MaxCandidates];
            var bottomCount = 0;
            var topCount = 0;
            Collect(board, bottom, top, ref bottomCount, ref topCount);

            var weight = bottomSpawnWeight;
            if (weight < 0f)
            {
                weight = 0f;
            }
            else if (weight > 1f)
            {
                weight = 1f;
            }

            var written = 0;
            while (written < count && written < results.Length && (bottomCount > 0 || topCount > 0))
            {
                if (!TryPickWeighted(
                        bottom,
                        ref bottomCount,
                        top,
                        ref topCount,
                        board.Width,
                        weight,
                        next,
                        out var slot))
                {
                    break;
                }

                results[written++] = slot;
            }

            return written;
        }

        public static bool TryPickRandomSlot(
            ISpawnBoardQuery board,
            float bottomSpawnWeight,
            Func<int, int, int> next,
            out SpawnCellSlot slot)
        {
            var buffer = new SpawnCellSlot[1];
            var n = PickRandomSlots(board, 1, bottomSpawnWeight, next, buffer);
            if (n <= 0)
            {
                slot = default;
                return false;
            }

            slot = buffer[0];
            return true;
        }

        /// <summary>
        /// Edge-first refill (bottom preference), matching sequential attack spawn scan.
        /// </summary>
        public static bool TryPickAttackSlot(ISpawnBoardQuery board, out SpawnCellSlot slot)
        {
            slot = default;
            if (board == null)
            {
                return false;
            }

            var width = board.Width;
            var height = board.Height;
            var cellCount = width * height;
            if (width <= 0 || height <= 0 || cellCount <= 0)
            {
                return false;
            }

            for (var index = 0; index < cellCount; index++)
            {
                var x = index % width;
                var y = (height - 1) - index / width;
                if (!board.IsCellAllowed(x, y))
                {
                    continue;
                }

                if (board.CanPlaceBottom(x, y))
                {
                    slot = new SpawnCellSlot(x, y, isTop: false);
                    return true;
                }

                if (board.CanPlaceTop(x, y))
                {
                    slot = new SpawnCellSlot(x, y, isTop: true);
                    return true;
                }
            }

            return false;
        }

        static void Collect(
            ISpawnBoardQuery board,
            int[] bottom,
            int[] top,
            ref int bottomCount,
            ref int topCount)
        {
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    if (!board.IsCellAllowed(x, y))
                    {
                        continue;
                    }

                    if (board.CanPlaceBottom(x, y) && bottomCount < MaxCandidates)
                    {
                        bottom[bottomCount++] = Encode(x, y, board.Width);
                    }

                    if (board.CanPlaceTop(x, y) && topCount < MaxCandidates)
                    {
                        top[topCount++] = Encode(x, y, board.Width);
                    }
                }
            }
        }

        static bool TryPickWeighted(
            int[] bottom,
            ref int bottomCount,
            int[] top,
            ref int topCount,
            int width,
            float bottomWeight,
            Func<int, int, int> next,
            out SpawnCellSlot slot)
        {
            slot = default;
            var hasBottom = bottomCount > 0;
            var hasTop = topCount > 0;
            if (!hasBottom && !hasTop)
            {
                return false;
            }

            bool pickTop;
            if (hasBottom && !hasTop)
            {
                pickTop = false;
            }
            else if (!hasBottom)
            {
                pickTop = true;
            }
            else
            {
                // Map weight to permille without floating RNG API.
                var threshold = (int)(bottomWeight * 1000f);
                if (threshold < 0)
                {
                    threshold = 0;
                }
                else if (threshold > 1000)
                {
                    threshold = 1000;
                }

                pickTop = next(0, 1000) >= threshold;
            }

            if (pickTop)
            {
                var index = next(0, topCount);
                Decode(top[index], width, out var x, out var y);
                RemoveAt(top, ref topCount, index);
                slot = new SpawnCellSlot(x, y, isTop: true);
                return true;
            }

            {
                var index = next(0, bottomCount);
                Decode(bottom[index], width, out var x, out var y);
                RemoveAt(bottom, ref bottomCount, index);
                slot = new SpawnCellSlot(x, y, isTop: false);
                return true;
            }
        }

        static void RemoveAt(int[] cells, ref int count, int index)
        {
            cells[index] = cells[count - 1];
            count--;
        }

        static int Encode(int x, int y, int width) => y * width + x;

        static void Decode(int packed, int width, out int x, out int y)
        {
            x = packed % width;
            y = packed / width;
        }
    }
}
