using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class JumpDiceTransferPolicy
    {
        public static bool BlocksJumpTransferToOtherDice(DiceController standingDice) {
            return standingDice != null
                && standingDice.Capabilities.BlocksJumpTransferToOtherDice;
        }

        public static bool ShouldBlockDiceToDiceTransfer(
            bool isJumping,
            DiceController standingDice,
            DiceController targetDice) {
            return isJumping
                && standingDice != null
                && targetDice != null
                && targetDice != standingDice
                && BlocksJumpTransferToOtherDice(standingDice);
        }
    }
}
