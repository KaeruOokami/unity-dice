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

        public static float GetTransferReachY(
            float arcReachY,
            BoardSurface fromSurface,
            bool isJumping,
            DiceController standingDice) {
            if (!UsesPlayerOnlyReach(isJumping, standingDice)) {
                return arcReachY;
            }

            return fromSurface.SurfaceWorldY;
        }
    }
}
