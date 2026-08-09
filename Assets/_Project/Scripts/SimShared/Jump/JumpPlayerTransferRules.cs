namespace DiceGame.SimShared.Jump
{
    using DiceGame.Core;
    using DiceGame.SimShared.Placement;

    /// <summary>
    /// Copied from production <c>JumpPlayerTransferPolicy</c> (DiceController → bool/caps facts).
    /// </summary>
    public static class JumpPlayerTransferRules
    {
        public static DiceStandingMoveMode ResolveStandingMoveMode(
            bool isJumping,
            bool hasStandingDice,
            bool isPlayerMovable,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing,
            DiceCapabilities capabilities)
        {
            if (!hasStandingDice)
            {
                return DiceStandingMoveMode.None;
            }

            return DiceBehaviorResolver.ResolveStandingMoveMode(
                capabilities,
                isJumping,
                isPlayerMovable,
                canJumpCoupleWithPlayer,
                isSinkErasing);
        }

        public static bool UsesPlayerOnlyMovement(DiceStandingMoveMode mode)
        {
            return mode == DiceStandingMoveMode.PlayerOnly;
        }

        public static bool ShouldEvaluateDiceCoupledMovement(DiceStandingMoveMode mode)
        {
            return mode == DiceStandingMoveMode.Slide || mode == DiceStandingMoveMode.Roll;
        }

        public static bool UsesPlayerOnlyReach(bool isJumping, DiceStandingMoveMode mode)
        {
            return isJumping && mode == DiceStandingMoveMode.PlayerOnly;
        }

        public static bool IsLowerLevelTransfer(int fromLevel, int targetLevel)
        {
            return targetLevel < fromLevel;
        }

        /// <summary>
        /// Sink-erasing, or player-only immovable (Iron / iron-adjacent Magnet).
        /// Stone excluded: CanGridRoll keeps it player-movable without jump-couple.
        /// </summary>
        public static bool RequiresJumpForLowerLevelTransfer(
            bool isSinkErasing,
            bool canJumpCoupleWithPlayer,
            bool isPlayerMovable)
        {
            return isSinkErasing
                || (!canJumpCoupleWithPlayer && !isPlayerMovable);
        }

        public static bool BlocksGroundLowerLevelTransfer(
            bool isJumping,
            int fromLevel,
            int targetLevel,
            bool isSinkErasing,
            bool canJumpCoupleWithPlayer,
            bool isPlayerMovable)
        {
            return !isJumping
                && IsLowerLevelTransfer(fromLevel, targetLevel)
                && RequiresJumpForLowerLevelTransfer(
                    isSinkErasing,
                    canJumpCoupleWithPlayer,
                    isPlayerMovable);
        }

        public static bool CanUsePlayerOnlyLowerLevelJump(
            bool isJumping,
            bool isSinkErasing,
            bool canJumpCoupleWithPlayer,
            bool isPlayerMovable)
        {
            return isJumping
                && RequiresJumpForLowerLevelTransfer(
                    isSinkErasing,
                    canJumpCoupleWithPlayer,
                    isPlayerMovable);
        }

        public static bool BlocksPlayerOnlyJumpLowerLevelTransfer(
            bool isJumping,
            int fromLevel,
            int targetLevel,
            DiceStandingMoveMode mode,
            bool isSinkErasing,
            bool canJumpCoupleWithPlayer,
            bool isPlayerMovable)
        {
            return isJumping
                && IsLowerLevelTransfer(fromLevel, targetLevel)
                && UsesPlayerOnlyMovement(mode)
                && !CanUsePlayerOnlyLowerLevelJump(
                    isJumping,
                    isSinkErasing,
                    canJumpCoupleWithPlayer,
                    isPlayerMovable);
        }

        public static bool ShouldUseTierLandingPolicy(int fromLevel, int targetLevel)
        {
            return fromLevel == SurfaceHeightNorms.Bottom && targetLevel == SurfaceHeightNorms.Top;
        }

        public static bool UsesCoupledJumpStep(
            bool isJumping,
            int fromLevel,
            bool hasStandingDice,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing)
        {
            if (!isJumping || fromLevel == SurfaceHeightNorms.Floor)
            {
                return false;
            }

            return hasStandingDice
                && canJumpCoupleWithPlayer
                && !isSinkErasing;
        }

        public static bool UsesPlayerOnlyJumpStep(
            bool isJumping,
            int fromLevel,
            bool hasStandingDice,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing)
        {
            return isJumping
                && !UsesCoupledJumpStep(
                    isJumping,
                    fromLevel,
                    hasStandingDice,
                    canJumpCoupleWithPlayer,
                    isSinkErasing);
        }
    }
}
