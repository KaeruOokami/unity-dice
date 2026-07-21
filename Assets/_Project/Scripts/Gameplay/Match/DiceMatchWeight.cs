using System.Collections.Generic;
using DiceGame.Core;

namespace DiceGame.Gameplay
{
    public static class DiceMatchWeight
    {
        public static int Get(DiceController dice, DiceStackTier matchTier) {
            if (dice == null) {
                return 0;
            }

            var capabilities = dice.Capabilities;
            if (!capabilities.HasExpandedFootprint) {
                return dice.CurrentState.Tier == matchTier ? 1 : 0;
            }

            if (dice.IsSinkErasing) {
                if (!capabilities.ParticipatesInBothTiersWhileSinking
                    && dice.CurrentState.Tier != matchTier) {
                    return 0;
                }

                var sinkingWeight = capabilities.SinkingMatchWeightPerTier;
                return sinkingWeight > 0
                    ? sinkingWeight
                    : JumboFootprint.MatchWeightPerTierWhileErasing;
            }

            // Pre-sink weight is applied once via GetPreSinkBridgedWeight in the bridged finder.
            // Per-tier Get returns 0 so the sinking/same-tier pass ignores pre-sink jumbos.
            return 0;
        }

        /// <summary>
        /// Pre-sink jumbo counts as one die; normals count as 1.
        /// </summary>
        public static int GetPreSinkBridgedWeight(DiceController dice) {
            if (dice == null) {
                return 0;
            }

            if (dice.Capabilities.HasExpandedFootprint && !dice.IsSinkErasing) {
                return JumboFootprint.MatchWeightBeforeErasure;
            }

            return 1;
        }

        public static int Sum(IReadOnlyList<DiceController> members, DiceStackTier matchTier) {
            var total = 0;
            if (members == null) {
                return total;
            }

            for (var i = 0; i < members.Count; i++) {
                total += Get(members[i], matchTier);
            }

            return total;
        }
    }
}
