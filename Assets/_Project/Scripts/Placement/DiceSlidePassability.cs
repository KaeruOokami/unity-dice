using DiceGame.Core;

namespace DiceGame.Placement
{
    public static class DiceSlidePassability
    {
        public static bool TryEvaluate(
            DiceState fromState,
            Direction direction,
            IDicePlacement placement,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (fromState.Tier == DiceStackTier.Bottom) {
                return TryEvaluateBottomSlide(fromState, direction, placement, out plan, out rejectReason);
            }

            if (fromState.Tier == DiceStackTier.Top) {
                return TryEvaluateTopSlide(fromState, direction, placement, out plan, out rejectReason);
            }

            rejectReason = $"unsupported-tier tier={fromState.Tier}";
            return false;
        }

        static bool TryEvaluateBottomSlide(
            DiceState fromState,
            Direction direction,
            IDicePlacement placement,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            var targetPos = fromState.GridPos + direction.ToGridDelta();
            if (!placement.CanPlaceBottomDiceAt(targetPos)) {
                rejectReason = $"target={FormatGrid(targetPos)} occupied";
                return false;
            }

            plan = new DiceSlidePlan(
                fromState,
                new DiceState(targetPos, fromState.Orientation, DiceStackTier.Bottom, fromState.Kind));
            return true;
        }

        static bool TryEvaluateTopSlide(
            DiceState fromState,
            Direction direction,
            IDicePlacement placement,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            var targetPos = fromState.GridPos + direction.ToGridDelta();
            if (placement.CanPlaceTopDiceAt(targetPos)) {
                plan = new DiceSlidePlan(
                    fromState,
                    new DiceState(targetPos, fromState.Orientation, DiceStackTier.Top, fromState.Kind));
                return true;
            }

            if (placement.CanPlaceBottomDiceAt(targetPos)) {
                plan = new DiceSlidePlan(
                    fromState,
                    new DiceState(targetPos, fromState.Orientation, DiceStackTier.Bottom, fromState.Kind));
                return true;
            }

            rejectReason = $"target={FormatGrid(targetPos)} blocked";
            return false;
        }

        static string FormatGrid(UnityEngine.Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
