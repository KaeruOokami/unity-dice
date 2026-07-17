using System;

namespace DiceGame.Core
{
    [Serializable]
    public struct DiceOrientation : IEquatable<DiceOrientation>
    {
        public int Top;
        public int North;
        public int East;

        public DiceOrientation(int top, int north, int east) {
            Top = top;
            North = north;
            East = east;
        }

        public static DiceOrientation Default => new(1, 2, 3);

        public static DiceOrientation CreateWithTopFace(int topFace) {
            if (topFace is < 1 or > 6) {
                return Default;
            }

            var directions = new[] { Direction.East, Direction.West, Direction.North, Direction.South };
            var visited = new System.Collections.Generic.HashSet<(int, int, int)>();
            var queue = new System.Collections.Generic.Queue<DiceOrientation>();
            queue.Enqueue(Default);

            while (queue.Count > 0) {
                var current = queue.Dequeue();
                var key = (current.Top, current.North, current.East);
                if (!visited.Add(key)) {
                    continue;
                }

                if (current.Top == topFace) {
                    return current;
                }

                for (var i = 0; i < directions.Length; i++) {
                    queue.Enqueue(current.Roll(directions[i]));
                }
            }

            return Default;
        }

        public int South => OppositeFace(North);
        public int West => OppositeFace(East);
        public int Down => OppositeFace(Top);

        public static int OppositeFace(int face) => 7 - face;

        public bool IsValid() {
            if (Top is < 1 or > 6) return false;
            if (North is < 1 or > 6) return false;
            if (East is < 1 or > 6) return false;
            if (Top == North || Top == East || North == East) return false;
            if (Top == South || Top == West || North == West || North == South) return false;
            if (East == South || East == West || South == West) return false;
            return true;
        }

        public DiceOrientation Roll(Direction direction) {
            var currentRotation = DiceOrientationMapper.ToRotation(this);
            var rollRotation = DiceRollTransform.GetRollRotation(direction);
            return DiceOrientationMapper.FromRotation(rollRotation * currentRotation);
        }

        public bool Equals(DiceOrientation other) {
            return Top == other.Top && North == other.North && East == other.East;
        }

        public override bool Equals(object obj) {
            return obj is DiceOrientation other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Top, North, East);
        }

        public override string ToString() {
            return $"Top={Top}, North={North}, East={East}";
        }
    }
}
