using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class JumpPlayerTransferPolicy
    {
        /// <summary>
        /// L1: Player moves alone; standing dice must not enter dice-coupled (L2) policies.
        /// Jump uses jump-coupling capability; ground uses general player-movable capability.
        /// </summary>
        public static bool UsesPlayerOnlyMovement(bool isJumping, DiceController standingDice) {
            if (standingDice == null) {
                return false;
            }

            if (isJumping) {
                return !standingDice.CanJumpCoupleWithPlayer;
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
                && !standingDice.CanJumpCoupleWithPlayer;
        }
    }
}
