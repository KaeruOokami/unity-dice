namespace DiceGame.SimShared.Jump
{
    using DiceGame.Core;
    using DiceGame.SimShared.GridMove;
    using DiceGame.SimShared.Motion;

    /// <summary>
    /// Copied from production <c>DiceCharacterCoupling</c> jump-grid session flags / begin gates.
    /// </summary>
    public static class JumpCoupleSession
    {
        public enum JumpDiceMoveKind
        {
            None = 0,
            SameTierParallel = 1,
            StackOntoTop = 2,
            DemoteToBottom = 3
        }

        public struct State
        {
            public bool IsJumpArc;
            public bool JumpDiceGridMoved;
            public JumpDiceMoveKind JumpMoveKind;
        }

        public static void ResetJumpSessionFlags(ref State state)
        {
            state.JumpDiceGridMoved = false;
            state.IsJumpArc = false;
            state.JumpMoveKind = JumpDiceMoveKind.None;
        }

        public static JumpDiceMoveKind ToMoveKind(DiceGridMoveKind kind)
        {
            return kind switch
            {
                DiceGridMoveKind.Parallel => JumpDiceMoveKind.SameTierParallel,
                DiceGridMoveKind.Stack => JumpDiceMoveKind.StackOntoTop,
                DiceGridMoveKind.Demote => JumpDiceMoveKind.DemoteToBottom,
                _ => JumpDiceMoveKind.None
            };
        }

        /// <summary>
        /// Copied from <c>TryBeginJumpGridMove</c> pre-execute gates (dice busy checked by caller).
        /// </summary>
        public static bool CanBeginJumpGridMove(in State state)
        {
            return !state.JumpDiceGridMoved;
        }

        public static void MarkJumpGridMoveStarted(ref State state, DiceGridMoveKind planKind)
        {
            state.IsJumpArc = true;
            state.JumpDiceGridMoved = true;
            state.JumpMoveKind = ToMoveKind(planKind);
        }

        public static void ClearJumpArc(ref State state)
        {
            state.IsJumpArc = false;
            state.JumpMoveKind = JumpDiceMoveKind.None;
        }
    }

    /// <summary>
    /// Copied from production <c>CharacterController.ShouldApplyJumpYOffsetToCharacter</c>.
    /// </summary>
    public static class JumpVisualOffsetRules
    {
        public static bool ShouldApplyJumpYOffsetToCharacter(
            bool isJumping,
            bool isOnFloor,
            bool hasStandingDice,
            bool canJumpCoupleWithPlayer,
            bool standingIsSinkErasing)
        {
            if (!isJumping)
            {
                return false;
            }

            if (isOnFloor)
            {
                return true;
            }

            if (!hasStandingDice)
            {
                return false;
            }

            if (!canJumpCoupleWithPlayer)
            {
                return true;
            }

            return standingIsSinkErasing;
        }
    }

    /// <summary>
    /// Copied production UpdateJump hold rules.
    /// </summary>
    public static class JumpLandingHoldRules
    {
        /// <summary>
        /// Freeze gravity step: JumpDiceGridMoved &amp;&amp; dice rolling &amp;&amp; !IsJumpArc.
        /// </summary>
        public static bool ShouldFreezeJumpStep(bool jumpDiceGridMoved, bool diceMotionBusy, bool isJumpArc)
        {
            return jumpDiceGridMoved && diceMotionBusy && !isJumpArc;
        }

        /// <summary>
        /// Hold EndJump after grounded: IsJumpArc &amp;&amp; dice still rolling.
        /// </summary>
        public static bool ShouldHoldEndJump(bool isJumpArc, bool diceMotionBusy)
        {
            return isJumpArc && diceMotionBusy;
        }
    }
}
