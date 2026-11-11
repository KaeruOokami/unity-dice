namespace DiceGame.Core
{
    /// <summary>
    /// Shared eligibility for carrying / receiving fire. Jumbo and Ghost never carry fire.
    /// </summary>
    public static class DiceFirePolicy
    {
        public static bool CanCarryFire(IDiceBehavior behavior) {
            if (behavior == null) {
                return false;
            }

            var caps = behavior.Capabilities;
            if (caps.HasExpandedFootprint || caps.IsPlayerPassThrough) {
                return false;
            }

            return true;
        }

        public static bool CanCarryFire(DiceKind kind) {
            return CanCarryFire(DiceBehaviorResolver.GetBehavior(kind));
        }
    }
}
