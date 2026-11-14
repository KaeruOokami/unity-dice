namespace DiceGame.Core
{
    /// <summary>
    /// Kind-static dice behavior. Look here for what a kind can do; runtime overlays use
    /// <see cref="EffectiveDiceBehavior"/>.
    /// </summary>
    public interface IDiceBehavior
    {
        DiceKind Kind { get; }
        DiceCapabilities Capabilities { get; }

        /// <summary>
        /// Maps physical top face to the face used for match / erasure / one-vanish.
        /// Default is identity; Reverse uses opposite faces.
        /// </summary>
        int ResolveMatchFace(int topFace);

        DiceStandingMoveMode ResolveStandingMoveMode(
            bool isJumping,
            bool isPlayerMovable,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing);

        DicePushMoveStyle ResolvePushStyle();
    }
}
