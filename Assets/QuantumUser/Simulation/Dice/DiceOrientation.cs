namespace Quantum
{
    /// <summary>
    /// Pure-integer orientation helpers (no Unity Quaternion). Matches <c>DiceOrientation</c> face rules.
    /// </summary>
    public static class DiceOrientation
    {
        public const int MinFace = 1;
        public const int MaxFace = 6;

        public static void Default(out int top, out int north, out int east)
        {
            top = 1;
            north = 2;
            east = 3;
        }

        public static void CreateWithTopFace(int topFace, out int top, out int north, out int east)
        {
            if (topFace < MinFace || topFace > MaxFace)
            {
                Default(out top, out north, out east);
                return;
            }

            Default(out top, out north, out east);
            // BFS over orthogonal rolls until Top matches the requested face.
            var queueTop = new int[24];
            var queueNorth = new int[24];
            var queueEast = new int[24];
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

                if (t == topFace)
                {
                    top = t;
                    north = n;
                    east = e;
                    return;
                }

                var key = ((t * 7) + n) * 7 + e;
                if (visited[key])
                {
                    continue;
                }

                visited[key] = true;

                EnqueueRoll(queueTop, queueNorth, queueEast, ref tail, RollNorth(t, n, e));
                EnqueueRoll(queueTop, queueNorth, queueEast, ref tail, RollEast(t, n, e));
                EnqueueRoll(queueTop, queueNorth, queueEast, ref tail, RollSouth(t, n, e));
                EnqueueRoll(queueTop, queueNorth, queueEast, ref tail, RollWest(t, n, e));
            }

            Default(out top, out north, out east);
        }

        static (int top, int north, int east) RollNorth(int top, int north, int east)
        {
            // Pitch forward: Top <- South, North <- Top, East unchanged.
            var south = Opposite(north);
            return (south, top, east);
        }

        static (int top, int north, int east) RollSouth(int top, int north, int east)
        {
            var south = Opposite(north);
            return (north, south, east);
        }

        static (int top, int north, int east) RollEast(int top, int north, int east)
        {
            var west = Opposite(east);
            return (west, north, top);
        }

        static (int top, int north, int east) RollWest(int top, int north, int east)
        {
            return (east, north, Opposite(top));
        }

        static int Opposite(int face) => 7 - face;

        static void EnqueueRoll(
            int[] queueTop,
            int[] queueNorth,
            int[] queueEast,
            ref int tail,
            (int top, int north, int east) next)
        {
            if (tail >= queueTop.Length)
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
