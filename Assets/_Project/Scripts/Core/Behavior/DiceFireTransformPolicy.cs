namespace DiceGame.Core
{
    /// <summary>
    /// Kind changes tied to fire burn-complete and RedHot cooling.
    /// </summary>
    public static class DiceFireTransformPolicy
    {
        /// <summary>
        /// Applied only when fire reaches full duration (not early extinguish).
        /// </summary>
        public static bool TryResolveBurnAftermath(DiceKind kind, out DiceKind nextKind) {
            nextKind = kind switch {
                DiceKind.Wood => DiceKind.Charcoal,
                DiceKind.Iron => DiceKind.RedHot,
                DiceKind.Ice => DiceKind.Normal,
                DiceKind.Magnet => DiceKind.Iron,
                _ => kind
            };
            return nextKind != kind;
        }

        /// <summary>
        /// RedHot cool-complete or standing-stun cool → Iron.
        /// </summary>
        public static bool TryResolveRedHotCool(DiceKind kind, out DiceKind nextKind) {
            nextKind = kind == DiceKind.RedHot ? DiceKind.Iron : kind;
            return nextKind != kind;
        }
    }
}
