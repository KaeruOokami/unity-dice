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
        public DiceKind Kind;

        public DiceState(
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceStackTier tier = DiceStackTier.Bottom,
            DiceKind kind = DiceKind.Normal) {
            GridPos = gridPos;
            Orientation = orientation;
            Tier = tier;
            Kind = kind;
        }

        public bool Equals(DiceState other) {
            return GridPos == other.GridPos
                && Orientation.Equals(other.Orientation)
                && Tier == other.Tier
                && Kind == other.Kind;
        }

        public override bool Equals(object obj) {
            return obj is DiceState other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(GridPos, Orientation, Tier, Kind);
        }

        public override string ToString() {
            return $"({GridPos.x}, {GridPos.y}) {Tier} {Kind} {Orientation}";
        }
    }
}
