using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class JumpPlayerTransferPolicy
    {
        /// <summary>
        /// Resolves L1/L2 standing-move mode from effective dice behavior.
        /// </summary>
        public static DiceStandingMoveMode ResolveStandingMoveMode(
            bool isJumping,
            DiceController standingDice) {
            if (standingDice == null) {
                return DiceStandingMoveMode.None;
            }

            return standingDice.EffectiveBehavior.ResolveStandingMoveMode(isJumping);
        }

        /// <summary>
        /// L1: Player moves alone; standing dice must not enter dice-coupled (L2) policies.
        /// </summary>
        public static bool UsesPlayerOnlyMovement(bool isJumping, DiceController standingDice) {
            return ResolveStandingMoveMode(isJumping, standingDice) == DiceStandingMoveMode.PlayerOnly;
        }

        /// <summary>
        /// L2 gate: dice-coupled policies run only for Slide or Roll modes.
        /// </summary>
        public static bool ShouldEvaluateDiceCoupledMovement(bool isJumping, DiceController standingDice) {
            var mode = ResolveStandingMoveMode(isJumping, standingDice);
            return mode == DiceStandingMoveMode.Slide || mode == DiceStandingMoveMode.Roll;
        }

        public static bool UsesPlayerOnlyReach(bool isJumping, DiceController standingDice) {
            return isJumping
                && standingDice != null
                && ResolveStandingMoveMode(true, standingDice) == DiceStandingMoveMode.PlayerOnly;
        }

        public static bool IsLowerLevelTransfer(int fromLevel, int targetLevel) {
            return targetLevel < fromLevel;
        }

        /// <summary>
        /// Sink-erasing dice, or player-only immovable dice (Iron / iron-adjacent Magnet).
        /// Stone is excluded: it remains player-movable via CanGridRoll even though it cannot jump-couple.
        /// </summary>
        public static bool RequiresJumpForLowerLevelTransfer(DiceController standingDice) {
            if (standingDice == null) {
                return false;
            }

            return standingDice.IsSinkErasing
                || (!standingDice.CanJumpCoupleWithPlayer && !standingDice.IsPlayerMovable);
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
