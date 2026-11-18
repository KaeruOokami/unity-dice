namespace DiceGame.Core
{
    /// <summary>
    /// Applies runtime modifiers to a kind-static <see cref="IDiceBehavior"/>.
    /// </summary>
    public static class DiceEffectiveBehaviorResolver
    {
        public static EffectiveDiceBehavior Resolve(
            IDiceBehavior baseBehavior,
            in DiceBehaviorRuntimeState state) {
            if (baseBehavior == null) {
                return default;
            }

            var caps = baseBehavior.Capabilities;
            var isErasingStanding = state.IsSinkErasing || state.IsRadianceErasing;
            var isPlayerPassThrough = caps.IsPlayerPassThrough && !isErasingStanding;

            // Erasing standing (Sink/Radiance) and spawning: solo-leave / non-couple.
            if (isErasingStanding || state.IsSpawning) {
                return new EffectiveDiceBehavior(
                    baseBehavior,
                    state.IsSinkErasing,
                    isPlayerPassThrough,
                    isPlayerMovable: false,
                    canJumpCoupleWithPlayer: false);
            }

            var isPlayerMovable = ResolvePlayerMovable(caps, state.HasAdjacentMagnetBlocker);
            var canJumpCouple = ResolveJumpCouple(caps, state.HasAdjacentMagnetBlocker);

            return new EffectiveDiceBehavior(
                baseBehavior,
                state.IsSinkErasing,
                isPlayerPassThrough,
                isPlayerMovable,
                canJumpCouple);
        }

        static bool ResolvePlayerMovable(DiceCapabilities caps, bool hasAdjacentMagnetBlocker) {
            if (!caps.CanBePushedByPlayer && !caps.CanGridRoll && !caps.SlideUntilBlocked) {
                return false;
            }

            if (!caps.HasMagnetCoupling) {
                return caps.CanBePushedByPlayer || caps.CanGridRoll || caps.SlideUntilBlocked;
            }

            return !hasAdjacentMagnetBlocker;
        }

        static bool ResolveJumpCouple(DiceCapabilities caps, bool hasAdjacentMagnetBlocker) {
            if (!caps.CanJumpCoupleWithPlayer) {
                return false;
            }

            if (caps.HasMagnetCoupling && hasAdjacentMagnetBlocker) {
                return false;
            }

            return true;
        }
    }
}
