namespace DiceGame.Placement
{
    public readonly struct HeightStepLimits
    {
        public float MaxWalkStep { get; }
        public float MaxJumpStepPlayerOnly { get; }
        public float MaxJumpStepCoupled { get; }

        public HeightStepLimits(
            float maxWalkStep,
            float maxJumpStepPlayerOnly,
            float maxJumpStepCoupled) {
            MaxWalkStep = maxWalkStep;
            MaxJumpStepPlayerOnly = maxJumpStepPlayerOnly;
            MaxJumpStepCoupled = maxJumpStepCoupled;
        }

        public float GetMaxStep(bool isJumping, bool isPlayerOnlyJump) {
            if (!isJumping) {
                return MaxWalkStep;
            }

            return isPlayerOnlyJump ? MaxJumpStepPlayerOnly : MaxJumpStepCoupled;
        }
    }
}
