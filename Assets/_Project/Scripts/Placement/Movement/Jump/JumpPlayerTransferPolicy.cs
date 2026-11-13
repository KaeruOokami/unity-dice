using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    /// <summary>
    /// Player vs dice standing-transfer facts.
    /// Descent from a non-coupleable standing surface is a player-entry rule
    /// (walk blocked, jump allowed) — not a Mode-specific special API.
    /// </summary>
    public static class JumpPlayerTransferPolicy
    {
        public static DiceStandingMoveMode ResolveStandingMoveMode(
            bool isJumping,
            DiceController standingDice) {
            if (standingDice == null) {
                return DiceStandingMoveMode.None;
            }

            return standingDice.EffectiveBehavior.ResolveStandingMoveMode(isJumping);
        }

        public static bool UsesPlayerOnlyMovement(bool isJumping, DiceController standingDice) {
            return ResolveStandingMoveMode(isJumping, standingDice) == DiceStandingMoveMode.PlayerOnly;
        }

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
        /// Standing surface the player cannot leave together with the die:
        /// Iron, iron-stuck Magnet, sink-erasing, and other effectively immovable non-couple dice.
        /// Stone (roll-capable, non-couple) is not this class — jump descent stays blocked separately.
        /// </summary>
        public static bool IsSoloLeaveStandingSurface(DiceController standingDice) {
            if (standingDice == null) {
                return false;
            }

            if (standingDice.IsSinkErasing) {
                return true;
            }

            if (standingDice.CanJumpCoupleWithPlayer) {
                return false;
            }

            return !standingDice.IsPlayerMovable;
        }

        /// <summary>
        /// Player may descend from the current standing die for this input.
        /// Solo-leave surfaces: jump only. Couple-denied but movable (Stone) on jump: never.
        /// Spawning: never.
        /// </summary>
        public static bool CanPlayerDescendFromStanding(bool isJumping, DiceController standingDice) {
            if (standingDice == null) {
                return true;
            }

            if (standingDice.IsSpawning) {
                return false;
            }

            if (IsSoloLeaveStandingSurface(standingDice)) {
                return isJumping;
            }

            // Jump while non-couple but still player-movable (Stone): no solo jump descent.
            if (isJumping && !standingDice.CanJumpCoupleWithPlayer) {
                return false;
            }

            return true;
        }

        public static bool BlocksPlayerDescentFromStanding(
            bool isJumping,
            int fromLevel,
            int targetLevel,
            DiceController standingDice) {
            return IsLowerLevelTransfer(fromLevel, targetLevel)
                && !CanPlayerDescendFromStanding(isJumping, standingDice);
        }

        /// <summary>
        /// True when jump descent from a solo-leave standing surface is allowed.
        /// </summary>
        public static bool CanUsePlayerOnlyLowerLevelJump(bool isJumping, DiceController standingDice) {
            return isJumping
                && standingDice != null
                && !standingDice.IsSpawning
                && IsSoloLeaveStandingSurface(standingDice);
        }

        public static bool BlocksGroundLowerLevelTransfer(
            bool isJumping,
            int fromLevel,
            int targetLevel,
            DiceController standingDice) {
            return !isJumping
                && IsLowerLevelTransfer(fromLevel, targetLevel)
                && IsSoloLeaveStandingSurface(standingDice);
        }

        public static bool BlocksPlayerOnlyJumpLowerLevelTransfer(
            bool isJumping,
            int fromLevel,
            int targetLevel,
            DiceController standingDice) {
            return isJumping
                && IsLowerLevelTransfer(fromLevel, targetLevel)
                && !CanPlayerDescendFromStanding(isJumping, standingDice);
        }

        public static bool ShouldUseTierLandingPolicy(int fromLevel, int targetLevel) {
            return fromLevel == SurfaceHeightLevel.Bottom && targetLevel == SurfaceHeightLevel.Top;
        }

        public static bool UsesCoupledJumpStep(bool isJumping, int fromLevel, DiceController standingDice) {
            if (!isJumping || fromLevel == SurfaceHeightLevel.Floor) {
                return false;
            }

            return standingDice != null
                && standingDice.CanJumpCoupleWithPlayer
                && !standingDice.IsSinkErasing;
        }

        public static bool UsesPlayerOnlyJumpStep(bool isJumping, int fromLevel, DiceController standingDice) {
            return isJumping && !UsesCoupledJumpStep(isJumping, fromLevel, standingDice);
        }
    }
}
