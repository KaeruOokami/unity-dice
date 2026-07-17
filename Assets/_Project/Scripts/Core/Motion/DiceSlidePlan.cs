namespace DiceGame.Core
{
    public readonly struct DiceSlidePlan
    {
        public DiceState From { get; }
        public DiceState To { get; }
        public GhostLandingMode GhostLanding { get; }
        public DiceState GhostFrom { get; }
        public DiceState GhostTo { get; }

        public bool HasGhostSwap => GhostLanding != GhostLandingMode.None;

        public DiceSlidePlan(DiceState from, DiceState to) {
            From = from;
            To = to;
            GhostLanding = GhostLandingMode.None;
            GhostFrom = default;
            GhostTo = default;
        }

        public DiceSlidePlan(
            DiceState from,
            DiceState to,
            GhostLandingMode ghostLanding,
            DiceState ghostFrom,
            DiceState ghostTo) {
            From = from;
            To = to;
            GhostLanding = ghostLanding;
            GhostFrom = ghostFrom;
            GhostTo = ghostTo;
        }

        public static DiceSlidePlan WithRetargetedFrom(DiceSlidePlan plan, DiceState from) {
            return new DiceSlidePlan(
                from,
                plan.To,
                plan.GhostLanding,
                plan.GhostFrom,
                plan.GhostTo);
        }
    }
}
