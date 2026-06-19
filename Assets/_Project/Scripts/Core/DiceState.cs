using System;
using UnityEngine;

namespace DiceGame.Core
{
    [Serializable]
    public struct DiceState : IEquatable<DiceState>
    {
        public Vector2Int GridPos;
        public DiceOrientation Orientation;
        public DiceStackTier Tier;

        public DiceState(Vector2Int gridPos, DiceOrientation orientation, DiceStackTier tier = DiceStackTier.Bottom) {
            GridPos = gridPos;
            Orientation = orientation;
            Tier = tier;
        }

        public bool Equals(DiceState other) {
            return GridPos == other.GridPos
                && Orientation.Equals(other.Orientation)
                && Tier == other.Tier;
        }

        public override bool Equals(object obj) {
            return obj is DiceState other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(GridPos, Orientation, Tier);
        }

        public override string ToString() {
            return $"({GridPos.x}, {GridPos.y}) {Tier} {Orientation}";
        }
    }
}
