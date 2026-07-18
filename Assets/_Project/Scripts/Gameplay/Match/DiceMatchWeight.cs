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

            // Jumbo: Bottom 4 + Top 4 both before and during erasure.
            if (!capabilities.ParticipatesInBothTiersWhileSinking
                && dice.CurrentState.Tier != matchTier) {
                return 0;
            }

            var weight = capabilities.SinkingMatchWeightPerTier;
            return weight > 0
                ? weight
                : JumboFootprint.MatchWeightPerTier;
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
