using System;
using UnityEngine;

namespace DiceGame.Core
{
    [Serializable]
    public struct DiceState : IEquatable<DiceState>
    {
        public Vector2Int GridPos;
        public DiceOrientation Orientation;

        public DiceState(Vector2Int gridPos, DiceOrientation orientation) {
            GridPos = gridPos;
            Orientation = orientation;
        }

        public bool Equals(DiceState other) {
            return GridPos == other.GridPos && Orientation.Equals(other.Orientation);
        }

        public override bool Equals(object obj) {
            return obj is DiceState other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(GridPos, Orientation);
        }

        public override string ToString() {
            return $"({GridPos.x}, {GridPos.y}) {Orientation}";
        }
    }
}
