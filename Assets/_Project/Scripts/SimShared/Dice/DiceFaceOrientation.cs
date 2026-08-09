namespace DiceGame.SimShared.Dice
{
    /// <summary>
    /// Pure-integer face triad helpers (opposites sum to 7).
    /// Shared by Quantum <c>DiceOrientation</c> and push/roll planning.
    /// </summary>
    public static class DiceFaceOrientation
    {
        public const int MinFace = 1;
        public const int MaxFace = 6;

        public static void Default(out int top, out int north, out int east)
        {
            top = 1;
            north = 2;
            east = 3;
        }

        public static bool IsValid(int top, int north, int east)
        {
            if (top < MinFace || top > MaxFace) return false;
            if (north < MinFace || north > MaxFace) return false;
            if (east < MinFace || east > MaxFace) return false;
            if (top == north || top == east || north == east) return false;

            var south = Opposite(north);
            var west = Opposite(east);
            if (top == south || top == west || north == west || north == south) return false;
            if (east == south || east == west || south == west) return false;
            return true;
        }

        public static void CreateWithTopFace(int topFace, out int top, out int north, out int east)
        {
            if (topFace < MinFace || topFace > MaxFace)
            {
                Default(out top, out north, out east);
                return;
            }

            Default(out top, out north, out east);
            if (top == topFace && IsValid(top, north, east))
            {
                return;
            }

            var queueTop = new int[48];
            var queueNorth = new int[48];
            var queueEast = new int[48];
            var visited = new bool[7 * 7 * 7];
            var head = 0;
            var tail = 0;
            queueTop[tail] = top;
            queueNorth[tail] = north;
            queueEast[tail] = east;
            tail++;

            while (head < tail)
            {
                var t = queueTop[head];
                var n = queueNorth[head];
                var e = queueEast[head];
                head++;

                var key = ((t * 7) + n) * 7 + e;
                if (visited[key])
                {
                    continue;
                }

                visited[key] = true;

                if (t == topFace && IsValid(t, n, e))
                {
                    top = t;
                    north = n;
                    east = e;
                    return;
                }

                TryEnqueue(queueTop, queueNorth, queueEast, ref tail, visited, RollNorth(t, n, e));
                TryEnqueue(queueTop, queueNorth, queueEast, ref tail, visited, RollEast(t, n, e));
                TryEnqueue(queueTop, queueNorth, queueEast, ref tail, visited, RollSouth(t, n, e));
                TryEnqueue(queueTop, queueNorth, queueEast, ref tail, visited, RollWest(t, n, e));
            }

            Default(out top, out north, out east);
        }

        /// <summary>Pitch forward: Top &lt;- South, North &lt;- Top, East unchanged.</summary>
        public static void RollNorth(int top, int north, int east, out int nextTop, out int nextNorth, out int nextEast)
        {
            var south = Opposite(north);
            nextTop = south;
            nextNorth = top;
            nextEast = east;
        }

        /// <summary>Pitch backward: Top &lt;- North, North &lt;- Down, East unchanged.</summary>
        public static void RollSouth(int top, int north, int east, out int nextTop, out int nextNorth, out int nextEast)
        {
            nextTop = north;
            nextNorth = Opposite(top);
            nextEast = east;
        }

        /// <summary>Yaw/roll right: Top &lt;- West, East &lt;- Top, North unchanged.</summary>
        public static void RollEast(int top, int north, int east, out int nextTop, out int nextNorth, out int nextEast)
        {
            var west = Opposite(east);
            nextTop = west;
            nextNorth = north;
            nextEast = top;
        }

        /// <summary>Yaw/roll left: Top &lt;- East, East &lt;- Down, North unchanged.</summary>
        public static void RollWest(int top, int north, int east, out int nextTop, out int nextNorth, out int nextEast)
        {
            nextTop = east;
            nextNorth = north;
            nextEast = Opposite(top);
        }

        public static bool TryRoll(
            int dirX,
            int dirY,
            int top,
            int north,
            int east,
            out int nextTop,
            out int nextNorth,
            out int nextEast)
        {
            if (dirX == 0 && dirY == 1)
            {
                RollNorth(top, north, east, out nextTop, out nextNorth, out nextEast);
                return true;
            }

            if (dirX == 0 && dirY == -1)
            {
                RollSouth(top, north, east, out nextTop, out nextNorth, out nextEast);
                return true;
            }

            if (dirX == 1 && dirY == 0)
            {
                RollEast(top, north, east, out nextTop, out nextNorth, out nextEast);
                return true;
            }

            if (dirX == -1 && dirY == 0)
            {
                RollWest(top, north, east, out nextTop, out nextNorth, out nextEast);
                return true;
            }

            nextTop = top;
            nextNorth = north;
            nextEast = east;
            return false;
        }

        public static int Opposite(int face) => 7 - face;

        static (int top, int north, int east) RollNorth(int top, int north, int east)
        {
            RollNorth(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }

        static (int top, int north, int east) RollSouth(int top, int north, int east)
        {
            RollSouth(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }

        static (int top, int north, int east) RollEast(int top, int north, int east)
        {
            RollEast(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }

        static (int top, int north, int east) RollWest(int top, int north, int east)
        {
            RollWest(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }

        static void TryEnqueue(
            int[] queueTop,
            int[] queueNorth,
            int[] queueEast,
            ref int tail,
            bool[] visited,
            (int top, int north, int east) next)
        {
            if (tail >= queueTop.Length || !IsValid(next.top, next.north, next.east))
            {
                return;
            }

            var key = ((next.top * 7) + next.north) * 7 + next.east;
            if (visited[key])
            {
                return;
            }

            queueTop[tail] = next.top;
            queueNorth[tail] = next.north;
            queueEast[tail] = next.east;
            tail++;
        }
    }
}
