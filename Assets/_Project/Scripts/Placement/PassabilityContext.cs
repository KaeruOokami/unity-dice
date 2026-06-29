namespace DiceGame.Placement
{
    public readonly struct PassabilityContext
    {
        public bool IsJumping { get; }
        public bool AllowJumpGridMove { get; }
        public bool AllowJumpTierChange { get; }
        public float EffectiveReachY { get; }

        PassabilityContext(
            bool isJumping,
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float effectiveReachY) {
            IsJumping = isJumping;
            AllowJumpGridMove = allowJumpGridMove;
            AllowJumpTierChange = allowJumpTierChange;
            EffectiveReachY = effectiveReachY;
        }

        public static PassabilityContext ForGround(float effectiveReachY) {
            return new PassabilityContext(false, false, false, effectiveReachY);
        }

        public static PassabilityContext Jump(
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float effectiveReachY) {
            return new PassabilityContext(true, allowJumpGridMove, allowJumpTierChange, effectiveReachY);
        }
    }
}
