namespace DiceGame.Placement
{
    public readonly struct HeightStepLimits
    {
        public float MaxWalkStep { get; }
        public float MaxJumpStep { get; }

        public HeightStepLimits(float maxWalkStep, float maxJumpStep) {
            MaxWalkStep = maxWalkStep;
            MaxJumpStep = maxJumpStep;
        }

        public float GetMaxStep(bool isJumping) {
            return isJumping ? MaxJumpStep : MaxWalkStep;
        }
    }
}
