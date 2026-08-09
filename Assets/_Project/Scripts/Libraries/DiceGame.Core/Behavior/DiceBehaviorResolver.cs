namespace DiceGame.Core
{
    public static class DiceBehaviorResolver
    {
        public static IDiceBehavior GetBehavior(DiceKind kind) {
            return kind switch {
                DiceKind.Wood => WoodDiceBehavior.Instance,
                DiceKind.Iron => IronDiceBehavior.Instance,
                DiceKind.Magnet => MagnetDiceBehavior.Instance,
                DiceKind.Ice => IceDiceBehavior.Instance,
                DiceKind.Stone => StoneDiceBehavior.Instance,
                DiceKind.Ghost => GhostDiceBehavior.Instance,
                DiceKind.Jumbo => JumboDiceBehavior.Instance,
                _ => NormalDiceBehavior.Instance
            };
        }

        public static DiceCapabilities GetCapabilities(DiceKind kind) {
            return GetBehavior(kind).Capabilities;
        }

        /// <summary>
        /// Prefer <see cref="IDiceBehavior.ResolveStandingMoveMode"/> or
        /// <see cref="EffectiveDiceBehavior.ResolveStandingMoveMode"/> at call sites.
        /// </summary>
        public static DiceStandingMoveMode ResolveStandingMoveMode(
            DiceCapabilities capabilities,
            bool isJumping,
            bool isPlayerMovable,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing) {
            // Compatibility wrapper: capabilities-only path without a behavior instance.
            if (isJumping) {
                if (isSinkErasing || !canJumpCoupleWithPlayer) {
                    return DiceStandingMoveMode.PlayerOnly;
                }
            } else if (!isPlayerMovable) {
                return DiceStandingMoveMode.PlayerOnly;
            }

            if (capabilities.SlideUntilBlocked) {
                return DiceStandingMoveMode.Slide;
            }

            if (capabilities.CanGridRoll) {
                return DiceStandingMoveMode.Roll;
            }

            return DiceStandingMoveMode.None;
        }
    }
}
