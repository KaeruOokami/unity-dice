namespace DiceGame.SimShared.Jump
{
    /// <summary>
    /// Copied from production <c>CharacterController.GetDiceJumpHeight</c>.
    /// </summary>
    public static class JumpHeightRules
    {
        public static float ResolveDiceJumpHeight(float cellSize, float jumpHeightDiceMultiplier, float jumpHeightFallback)
        {
            if (cellSize > 0f && jumpHeightDiceMultiplier > 0f)
            {
                return cellSize * jumpHeightDiceMultiplier;
            }

            return jumpHeightFallback > 0f ? jumpHeightFallback : 1f;
        }
    }
}
