namespace DiceGame.SimShared.Lift
{
    /// <summary>
    /// Production <c>LiftPassability.CanLift</c> without Unity controllers.
    /// </summary>
    public static class LiftEligibility
    {
        public static bool CanLift(
            bool isOnFloor,
            int standingTier,
            bool targetIsStandingDice,
            bool canBeLiftedByPlayer,
            bool isPlayerMovable,
            bool isCarried,
            bool isErasing,
            bool isBusy,
            bool isSpawning,
            int diceTier,
            bool hasTopOnDiceCell)
        {
            if (targetIsStandingDice)
            {
                return false;
            }

            if (isCarried || isErasing || isBusy || isSpawning)
            {
                return false;
            }

            if (!canBeLiftedByPlayer || !isPlayerMovable)
            {
                return false;
            }

            if (isOnFloor)
            {
                if (diceTier == 1)
                {
                    return true;
                }

                return diceTier == 0 && !hasTopOnDiceCell;
            }

            if (standingTier == 0)
            {
                return true;
            }

            return diceTier == 1;
        }

        /// <summary>
        /// Production lift is facing-neighbor only (<c>CharacterLiftTargetQuery</c>).
        /// </summary>
        public static bool HasFacing(int facingX, int facingY)
        {
            return facingX != 0 || facingY != 0;
        }
    }
}
