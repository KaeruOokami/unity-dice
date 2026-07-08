namespace DiceGame.Placement
{
    public readonly struct PassabilityContext
    {
        public bool IsJumping { get; }
        public bool AllowJumpGridMove { get; }
        public bool AllowJumpTierChange { get; }
        public float FootingWorldY { get; }

        PassabilityContext(
            bool isJumping,
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float footingWorldY) {
            IsJumping = isJumping;
            AllowJumpGridMove = allowJumpGridMove;
            AllowJumpTierChange = allowJumpTierChange;
            FootingWorldY = footingWorldY;
        }

        public static PassabilityContext ForGround(float footingWorldY) {
            return new PassabilityContext(false, false, false, footingWorldY);
        }

        public static PassabilityContext Jump(
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float footingWorldY) {
            return new PassabilityContext(true, allowJumpGridMove, allowJumpTierChange, footingWorldY);
        }
    }
}
