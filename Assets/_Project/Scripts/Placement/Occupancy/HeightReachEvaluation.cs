namespace DiceGame.Placement
{
    public readonly struct HeightReachEvaluation
    {
        public float FloorWorldY { get; }
        public float CellSize { get; }
        public HeightStepLimits Limits { get; }
        public bool IsJumping { get; }
        public bool IsPlayerOnlyJump { get; }

        public HeightReachEvaluation(
            float floorWorldY,
            float cellSize,
            HeightStepLimits limits,
            bool isJumping,
            bool isPlayerOnlyJump) {
            FloorWorldY = floorWorldY;
            CellSize = cellSize;
            Limits = limits;
            IsJumping = isJumping;
            IsPlayerOnlyJump = isPlayerOnlyJump;
        }

        public float GetMaxStepNorm() {
            return Limits.GetMaxStep(IsJumping, IsPlayerOnlyJump);
        }
    }
}
