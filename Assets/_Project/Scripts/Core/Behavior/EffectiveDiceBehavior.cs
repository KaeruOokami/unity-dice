namespace DiceGame.Core
{
    /// <summary>
    /// Kind behavior plus runtime modifiers (sink solidify, radiance/spawn lock, magnet+iron).
    /// Callers that care about "can the player move/couple with this die right now" should use this.
    /// </summary>
    public readonly struct EffectiveDiceBehavior
    {
        public IDiceBehavior Base { get; }
        public DiceCapabilities Capabilities => Base.Capabilities;
        public bool IsSinkErasing { get; }
        public bool IsPlayerPassThrough { get; }
        public bool IsPlayerMovable { get; }
        public bool CanJumpCoupleWithPlayer { get; }

        public EffectiveDiceBehavior(
            IDiceBehavior baseBehavior,
            bool isSinkErasing,
            bool isPlayerPassThrough,
            bool isPlayerMovable,
            bool canJumpCoupleWithPlayer) {
            Base = baseBehavior;
            IsSinkErasing = isSinkErasing;
            IsPlayerPassThrough = isPlayerPassThrough;
            IsPlayerMovable = isPlayerMovable;
            CanJumpCoupleWithPlayer = canJumpCoupleWithPlayer;
        }

        public DiceStandingMoveMode ResolveStandingMoveMode(bool isJumping) {
            return Base.ResolveStandingMoveMode(
                isJumping,
                IsPlayerMovable,
                CanJumpCoupleWithPlayer,
                IsSinkErasing);
        }

        public DicePushMoveStyle ResolvePushStyle() {
            return Base.ResolvePushStyle();
        }
    }
}
