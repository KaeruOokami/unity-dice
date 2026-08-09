using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.SimShared.Jump;

namespace DiceGame.Placement
{
    public static class JumpDiceTransferPolicy
    {
        public static bool BlocksJumpTransferToOtherDice(DiceController standingDice)
        {
            return standingDice != null
                && standingDice.Capabilities.BlocksJumpTransferToOtherDice;
        }

        public static bool ShouldBlockDiceToDiceTransfer(
            bool isJumping,
            DiceController standingDice,
            DiceController targetDice)
        {
            return JumpDiceTransferRules.ShouldBlockDiceToDiceTransfer(
                isJumping,
                standingBlocksJumpTransferToOtherDice: BlocksJumpTransferToOtherDice(standingDice),
                hasStandingDice: standingDice != null,
                hasTargetDice: targetDice != null,
                targetIsSameAsStanding: standingDice != null && targetDice == standingDice);
        }
    }
}
