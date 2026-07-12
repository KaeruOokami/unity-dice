using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class JumpPlayerTransferPolicy
    {
        static bool IsNonCoupledJumpDice(DiceController standingDice) {
            return standingDice.IsSinkErasing || !standingDice.CanJumpCoupleWithPlayer;
        }

        /// <summary>
        /// L1: Player moves alone; standing dice must not enter dice-coupled (L2) policies.
        /// Jump uses jump-coupling capability; ground uses general player-movable capability.
        /// </summary>
        public static bool UsesPlayerOnlyMovement(bool isJumping, DiceController standingDice) {
            if (standingDice == null) {
                return false;
            }

            if (isJumping) {
                return IsNonCoupledJumpDice(standingDice);
            }

            return !standingDice.IsPlayerMovable;
        }

        /// <summary>
        /// L2 gate: dice-coupled policies (roll / slide / top-fall) run only when this is true.
        /// </summary>
        public static bool ShouldEvaluateDiceCoupledMovement(bool isJumping, DiceController standingDice) {
            return standingDice != null
                && !UsesPlayerOnlyMovement(isJumping, standingDice);
        }

        public static bool UsesPlayerOnlyReach(bool isJumping, DiceController standingDice) {
            return isJumping
                && standingDice != null
                && IsNonCoupledJumpDice(standingDice);
        }

        public static bool IsLowerLevelTransfer(int fromLevel, int targetLevel) {
            return targetLevel < fromLevel;
        }

        /// <summary>
        /// Sink-erasing dice, or non-couple dice that cannot grid-roll (Iron / iron-adjacent Magnet).
        /// Stone is excluded because it can grid-roll.
        /// </summary>
        public static bool RequiresJumpForLowerLevelTransfer(DiceController standingDice) {
            if (standingDice == null) {
                return false;
            }

            return standingDice.IsSinkErasing
                || (!standingDice.CanJumpCoupleWithPlayer && !standingDice.Capabilities.CanGridRoll);
        }

        public static bool BlocksGroundLowerLevelTransfer(
            bool isJumping,
            int fromLevel,
            int targetLevel,
            DiceController standingDice) {
            return !isJumping
                && IsLowerLevelTransfer(fromLevel, targetLevel)
                && RequiresJumpForLowerLevelTransfer(standingDice);
        }

        public static bool CanUsePlayerOnlyLowerLevelJump(bool isJumping, DiceController standingDice) {
            return isJumping && RequiresJumpForLowerLevelTransfer(standingDice);
        }

        /// <summary>
        /// Roll-capable player-only dice (Stone): jump descent to a lower level is not allowed.
        /// </summary>
        public static bool BlocksPlayerOnlyJumpLowerLevelTransfer(
            bool isJumping,
            int fromLevel,
            int targetLevel,
            DiceController standingDice) {
            return isJumping
                && IsLowerLevelTransfer(fromLevel, targetLevel)
                && UsesPlayerOnlyMovement(isJumping, standingDice)
                && !CanUsePlayerOnlyLowerLevelJump(isJumping, standingDice);
        }

        public static bool ShouldUseTierLandingPolicy(int fromLevel, int targetLevel) {
            return fromLevel == SurfaceHeightLevel.Bottom && targetLevel == SurfaceHeightLevel.Top;
        }

        /// <summary>
        /// Logical jump reach: player+dice coupled jump from Bottom/Top with a couple-capable dice.
        /// </summary>
        public static bool UsesCoupledJumpStep(bool isJumping, int fromLevel, DiceController standingDice) {
            if (!isJumping || fromLevel == SurfaceHeightLevel.Floor) {
                return false;
            }

            return standingDice != null
                && standingDice.CanJumpCoupleWithPlayer
                && !standingDice.IsSinkErasing;
        }

        /// <summary>
        /// Logical jump reach: floor, sink-erasing dice, or non-couple dice (Iron / Stone / iron-adjacent Magnet, etc.).
        /// </summary>
        public static bool UsesPlayerOnlyJumpStep(bool isJumping, int fromLevel, DiceController standingDice) {
            return isJumping && !UsesCoupledJumpStep(isJumping, fromLevel, standingDice);
        }
    }
}
