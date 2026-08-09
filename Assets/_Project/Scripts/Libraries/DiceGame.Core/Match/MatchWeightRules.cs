namespace DiceGame.Core
{
    /// <summary>
    /// Match cluster weight rules (production <c>DiceMatchWeight</c>) without Unity controllers.
    /// </summary>
    public static class MatchWeightRules
    {
        public static int GetSameTier(
            bool hasExpandedFootprint,
            bool isSinkErasing,
            int diceTier,
            int matchTier,
            bool keepsJumboTopOccupancy,
            bool participatesInBothTiersWhileSinking,
            int sinkingMatchWeightPerTier)
        {
            if (!hasExpandedFootprint)
            {
                return diceTier == matchTier ? 1 : 0;
            }

            if (!isSinkErasing)
            {
                // Pre-sink jumbo handled only in bridged pass.
                return 0;
            }

            if (!participatesInBothTiersWhileSinking && diceTier != matchTier)
            {
                return 0;
            }

            if (matchTier == 1 && !keepsJumboTopOccupancy)
            {
                return 0;
            }

            return sinkingMatchWeightPerTier > 0
                ? sinkingMatchWeightPerTier
                : DiceBehaviorConstants.JumboSinkingMatchWeightPerTier;
        }

        public static int GetPreSinkBridged(bool hasExpandedFootprint, bool isSinkErasing)
        {
            if (hasExpandedFootprint && !isSinkErasing)
            {
                return JumboFootprintCells.MatchWeightBeforeErasure;
            }

            return 1;
        }
    }
}
