using DiceGame.Core;
using DiceGame.Gameplay;

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

            while (DiceSlidePassability.TryEvaluate(current, direction, placement, out var stepPlan, out stepRejectReason)) {
                steps++;
                current = stepPlan.To;
            }

            if (steps == 0) {
                rejectReason = stepRejectReason ?? "no-slide-step";
                return false;
            }

            plan = new DiceSlidePlan(fromState, current);
            return true;
        }
    }
}
