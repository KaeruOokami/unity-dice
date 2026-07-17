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

        DiceStandingMoveMode ResolveStandingMoveMode(
            bool isJumping,
            bool isPlayerMovable,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing);

        DicePushMoveStyle ResolvePushStyle();
    }
}
