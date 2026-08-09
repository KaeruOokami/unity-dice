namespace DiceGame.SimShared.Jump
{
    /// <summary>
    /// Pure jump dice-transfer gates (production <c>JumpDiceTransferPolicy</c>).
    /// </summary>
    public static class JumpDiceTransferRules
    {
        public static bool ShouldBlockDiceToDiceTransfer(
            bool isJumping,
            bool standingBlocksJumpTransferToOtherDice,
            bool hasStandingDice,
            bool hasTargetDice,
            bool targetIsSameAsStanding)
        {
            return isJumping
                && hasStandingDice
                && hasTargetDice
                && !targetIsSameAsStanding
                && standingBlocksJumpTransferToOtherDice;
        }
    }
}
