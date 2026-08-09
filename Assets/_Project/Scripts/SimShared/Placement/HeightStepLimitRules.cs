namespace DiceGame.SimShared.Placement
{
    /// <summary>
    /// Jump/walk max-step selection matching <c>HeightStepLimits.GetMaxStep</c>
    /// and <c>JumpPlayerTransferPolicy</c> couple vs player-only choice.
    /// </summary>
    public static class HeightStepLimitRules
    {
        public static int ResolveMaxStepPermille(
            int walkPermille,
            int jumpPlayerOnlyPermille,
            int jumpCoupledPermille,
            bool isJumping,
            bool useCoupledJumpStep)
        {
            if (!isJumping)
            {
                return walkPermille > 0 ? walkPermille : 0;
            }

            if (useCoupledJumpStep)
            {
                return jumpCoupledPermille > 0 ? jumpCoupledPermille : 0;
            }

            return jumpPlayerOnlyPermille > 0 ? jumpPlayerOnlyPermille : 0;
        }

        /// <summary>
        /// Coupled jump when jumping from a couple-capable non-sink dice.
        /// Prefer <c>JumpPlayerTransferRules.UsesCoupledJumpStep</c> at new call sites.
        /// </summary>
        public static bool UsesCoupledJumpStep(
            bool isJumping,
            bool isOnFloor,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing = false)
        {
            if (!isJumping || isOnFloor)
            {
                return false;
            }

            return canJumpCoupleWithPlayer && !isSinkErasing;
        }

        /// <summary>
        /// Sink or Iron-like: jump may descend freely (production RequiresJumpForLowerLevelTransfer).
        /// </summary>
        public static bool CanUsePlayerOnlyLowerLevelJump(
            bool isJumping,
            bool canJumpCoupleWithPlayer,
            bool isPlayerMovable,
            bool isSinkErasing = false)
        {
            return isJumping
                && (isSinkErasing || (!canJumpCoupleWithPlayer && !isPlayerMovable));
        }

        public static bool IsPlayerMovable(
            bool canBePushedByPlayer,
            bool canGridRoll,
            bool slideUntilBlocked)
        {
            return canBePushedByPlayer || canGridRoll || slideUntilBlocked;
        }
    }
}
