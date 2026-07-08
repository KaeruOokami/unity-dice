using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class JumpPlayerTransferPolicy
    {
        public static bool UsesPlayerOnlyReach(bool isJumping, DiceController standingDice) {
            return isJumping
                && standingDice != null
                && !standingDice.CanJumpCoupleWithPlayer;
        }
    }
}
