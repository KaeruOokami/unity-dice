using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    /// <summary>
    /// Player vs dice standing-transfer facts.
    /// Hard solo-leave (Iron / stuck Magnet): walk descent blocked, jump allowed.
    /// Jump solo-leave (!couple and no roll/slide accompany): jump descends without the die
    /// (Charcoal, Iron). Stone keeps CanGridRoll so it is not jump-solo.
    /// Erasing standing (Sink / Radiance): walk edge uses dissolve hold; jump descends immediately.
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
        /// Iron / stuck Magnet etc.: cannot leave with the die; walk descent hard-blocked.
        /// Erasing standing is not this class (uses dissolve hold on walk).
        /// </summary>
        public static bool IsHardSoloLeaveStandingSurface(DiceController standingDice) {
            if (standingDice == null || standingDice.IsErasingStandingSurface) {
                return false;
            }

            if (standingDice.CanJumpCoupleWithPlayer) {
                return false;
            }

            return !standingDice.IsPlayerMovable;
        }

        /// <summary>
        /// Jump cannot bring the die: no jump couple and no roll/slide accompany path.
        /// Charcoal and Iron match; Stone (CanGridRoll) does not.
        /// </summary>
        public static bool IsJumpSoloLeaveStandingSurface(DiceController standingDice) {
            if (standingDice == null || standingDice.IsErasingStandingSurface) {
                return false;
            }

            if (standingDice.CanJumpCoupleWithPlayer) {
                return false;
            }

            var caps = standingDice.Capabilities;
            return !caps.CanGridRoll && !caps.SlideUntilBlocked;
        }

        /// <summary>
        /// Player leaves without bringing the die: erasing, hard solo-leave, or jump solo-leave.
        /// </summary>
        public static bool IsSoloLeaveStandingSurface(DiceController standingDice) {
            return standingDice != null
                && (standingDice.IsErasingStandingSurface
                    || IsHardSoloLeaveStandingSurface(standingDice)
                    || IsJumpSoloLeaveStandingSurface(standingDice));
        }

        /// <summary>
        /// Player may start a descent for this input.
        /// Decision by input mode, then capability facts (not Kind branches).
        /// </summary>
        public static bool CanPlayerDescendFromStanding(bool isJumping, DiceController standingDice) {
            if (standingDice == null) {
                return true;
            }

            if (standingDice.IsSpawning) {
                return false;
            }

            if (standingDice.IsErasingStandingSurface) {
                return true;
            }

            if (isJumping) {
                if (standingDice.CanJumpCoupleWithPlayer) {
                    return true;
                }

                return IsJumpSoloLeaveStandingSurface(standingDice)
                    || IsHardSoloLeaveStandingSurface(standingDice);
            }

            return !IsHardSoloLeaveStandingSurface(standingDice);
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
        /// Immediate jump descent from solo-leave surfaces (hard, jump-solo, or erasing).
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
                && IsHardSoloLeaveStandingSurface(standingDice);
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
                && !standingDice.IsErasingStandingSurface;
        }

        public static bool UsesPlayerOnlyJumpStep(bool isJumping, int fromLevel, DiceController standingDice) {
            return isJumping && !UsesCoupledJumpStep(isJumping, fromLevel, standingDice);
        }
    }
}
