namespace DiceGame.SimShared.Jump
{
    /// <summary>
    /// Production <c>CharacterController.TryBeginJump</c> start gates (logical).
    /// </summary>
    public static class JumpBeginRules
    {
        public static bool CanBegin(
            bool isAlreadyJumping,
            bool hasCarriedDice,
            bool hasCoupledWalkRoll,
            bool hasPushFollow)
        {
            if (isAlreadyJumping || hasCarriedDice || hasCoupledWalkRoll || hasPushFollow)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Approximate airborne window from launch height + gravity (production GravityMotion).
        /// Full jump time ≈ 2 * sqrt(2h/g) at 60 Hz.
        /// </summary>
        public static int ResolveAirborneTicks(float jumpHeight, float gravity, int tickHz, int fallbackTicks)
        {
            if (jumpHeight <= 0f || gravity <= 0f || tickHz <= 0)
            {
                return fallbackTicks > 0 ? fallbackTicks : 1;
            }

            var seconds = 2f * (float)System.Math.Sqrt(2f * jumpHeight / gravity);
            var ticks = (int)System.Math.Round(seconds * tickHz);
            return ticks > 0 ? ticks : (fallbackTicks > 0 ? fallbackTicks : 1);
        }
    }
}
