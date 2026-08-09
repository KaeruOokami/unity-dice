namespace DiceGame.SimShared.Push
{
    /// <summary>
    /// Pure push eligibility matching production <c>PushPassability.CanPush</c>.
    /// Slide-until-blocked style is allowed; multi-cell planning is separate.
    /// </summary>
    public static class PushEligibility
    {
        public static bool CanPush(
            bool isOnFloor,
            int playerCellX,
            int playerCellY,
            int playerTier,
            int diceCellX,
            int diceCellY,
            int diceTier,
            bool canBePushedByPlayer,
            bool isCarried,
            bool isErasing,
            bool isMotionBusy,
            bool isStandingOnThisDice)
        {
            if (isStandingOnThisDice)
            {
                return false;
            }

            if (isCarried || isErasing || isMotionBusy)
            {
                return false;
            }

            if (!canBePushedByPlayer)
            {
                return false;
            }

            if (!PushAdjacency.IsAdjacentForPush(
                    playerCellX,
                    playerCellY,
                    playerTier,
                    isOnFloor,
                    diceCellX,
                    diceCellY,
                    diceTier))
            {
                return false;
            }

            if (isOnFloor)
            {
                return diceTier == 0;
            }

            return diceTier == 1;
        }
    }
}
