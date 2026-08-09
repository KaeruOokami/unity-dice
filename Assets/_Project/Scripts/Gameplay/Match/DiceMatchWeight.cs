using System.Collections.Generic;
using DiceGame.Core;

namespace DiceGame.Gameplay
{
    public static class DiceMatchWeight
    {
        public static int Get(DiceController dice, DiceStackTier matchTier)
        {
            if (dice == null)
            {
                return 0;
            }

            var capabilities = dice.Capabilities;
            return MatchWeightRules.GetSameTier(
                capabilities.HasExpandedFootprint,
                dice.IsSinkErasing,
                dice.CurrentState.Tier == DiceStackTier.Top ? 1 : 0,
                matchTier == DiceStackTier.Top ? 1 : 0,
                dice.KeepsJumboTopOccupancy,
                capabilities.ParticipatesInBothTiersWhileSinking,
                capabilities.SinkingMatchWeightPerTier);
        }

        public static int GetPreSinkBridgedWeight(DiceController dice)
        {
            if (dice == null)
            {
                return 0;
            }

            return MatchWeightRules.GetPreSinkBridged(
                dice.Capabilities.HasExpandedFootprint,
                dice.IsSinkErasing);
        }

        public static int Sum(IReadOnlyList<DiceController> members, DiceStackTier matchTier)
        {
            var total = 0;
            if (members == null)
            {
                return total;
            }

            for (var i = 0; i < members.Count; i++)
            {
                total += Get(members[i], matchTier);
            }

            return total;
        }
    }
}
