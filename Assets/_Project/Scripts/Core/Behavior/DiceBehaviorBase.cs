namespace DiceGame.Core
{
    public abstract class DiceBehaviorBase : IDiceBehavior
    {
        public abstract DiceKind Kind { get; }
        public abstract DiceCapabilities Capabilities { get; }

        public virtual DiceStandingMoveMode ResolveStandingMoveMode(
            bool isJumping,
            bool isPlayerMovable,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing) {
            if (isJumping) {
                if (isSinkErasing || !canJumpCoupleWithPlayer) {
                    return DiceStandingMoveMode.PlayerOnly;
                }
            } else if (!isPlayerMovable) {
                return DiceStandingMoveMode.PlayerOnly;
            }

            if (Capabilities.SlideUntilBlocked) {
                return DiceStandingMoveMode.Slide;
            }

            if (Capabilities.CanGridRoll) {
                return DiceStandingMoveMode.Roll;
            }

            return DiceStandingMoveMode.None;
        }

        public virtual DicePushMoveStyle ResolvePushStyle() {
            if (Capabilities.PushUsesRoll) {
                return DicePushMoveStyle.Roll;
            }

            if (Capabilities.SlideUntilBlocked) {
                return DicePushMoveStyle.SlideUntilBlocked;
            }

            return DicePushMoveStyle.SingleCellSlide;
        }
    }
}
