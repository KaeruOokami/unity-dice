using System.Collections.Generic;
using DiceGame.Core;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// AI view of a match gather cluster: same connectivity/weight as match,
    /// including incomplete clusters (weight may be below face).
    /// </summary>
    public readonly struct AiFaceCluster
    {
        public IReadOnlyList<DiceSnapshot> Members { get; }
        public int Weight { get; }
        public DiceStackTier MatchTier { get; }

        public int Count => Members != null ? Members.Count : 0;

        public AiFaceCluster(
            IReadOnlyList<DiceSnapshot> members,
            int weight,
            DiceStackTier matchTier) {
            Members = members ?? System.Array.Empty<DiceSnapshot>();
            Weight = weight;
            MatchTier = matchTier;
        }

        public bool IsImmediate(int face) {
            return face >= 2 && Weight >= face;
        }

        public int RemainingNeed(int face) {
            if (face < 2) {
                return 0;
            }

            var need = face - Weight;
            return need > 0 ? need : 0;
        }
    }
}
