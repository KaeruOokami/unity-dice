using DiceGame.Core;

namespace DiceGame.Placement
{
    public static class IceSlidePassability
    {
        public static bool TryBuildUntilBlocked(
            DiceState fromState,
            Direction direction,
            IDicePlacement placement,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            var current = fromState;
            var steps = 0;
            string stepRejectReason = null;
            DiceSlidePlan lastStep = default;

            while (DiceSlidePassability.TryEvaluate(current, direction, placement, out var stepPlan, out stepRejectReason)) {
                steps++;
                lastStep = stepPlan;

                if (stepPlan.HasGhostSwap) {
                    current = stepPlan.To;
                    break;
                }

                // If Ice falls to a lower tier, stop there.
                // (Top -> Bottom is considered a "fall" to a lower level.)
                if (current.Tier == DiceStackTier.Top
                    && stepPlan.To.Tier == DiceStackTier.Bottom) {
                    current = stepPlan.To;
                    break;
                }

                current = stepPlan.To;
            }

            if (steps == 0) {
                rejectReason = stepRejectReason ?? "no-slide-step";
                return false;
            }

            plan = lastStep.HasGhostSwap
                ? DiceSlidePlan.WithRetargetedFrom(lastStep, fromState)
                : new DiceSlidePlan(fromState, current);
            return true;
        }
    }
}
