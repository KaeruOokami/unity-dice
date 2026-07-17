using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class DiceSlidePassability
    {
        public static bool TryEvaluate(
            DiceState fromState,
            Direction direction,
            DiceRegistry registry,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (registry == null) {
                rejectReason = "no-registry";
                return false;
            }

            if (fromState.Tier == DiceStackTier.Bottom) {
                return TryEvaluateBottomSlide(fromState, direction, registry, out plan, out rejectReason);
            }

            if (fromState.Tier == DiceStackTier.Top) {
                return TryEvaluateTopSlide(fromState, direction, registry, out plan, out rejectReason);
            }

            rejectReason = $"unsupported-tier tier={fromState.Tier}";
            return false;
        }

        /// <summary>Compatibility wrapper for callers that only have <see cref="IDicePlacement"/>.</summary>
        public static bool TryEvaluate(
            DiceState fromState,
            Direction direction,
            IDicePlacement placement,
            out DiceSlidePlan plan,
            out string rejectReason) {
            if (placement is DiceRegistry registry) {
                return TryEvaluate(fromState, direction, registry, out plan, out rejectReason);
            }

            plan = default;
            rejectReason = "ghost-swap-requires-dice-registry";
            return false;
        }

        static bool TryEvaluateBottomSlide(
            DiceState fromState,
            Direction direction,
            DiceRegistry registry,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            var targetPos = fromState.GridPos + direction.ToGridDelta();
            if (registry.BlocksTraversalBetween(fromState.GridPos, targetPos)) {
                rejectReason = $"target={FormatGrid(targetPos)} blocked-by-partition";
                return false;
            }

            if (registry.CanPlaceBottomDiceAt(targetPos)) {
                plan = new DiceSlidePlan(
                    fromState,
                    new DiceState(targetPos, fromState.Orientation, DiceStackTier.Bottom, fromState.Kind));
                return true;
            }

            if (registry.TryGetBottomAt(targetPos, out var ghost)
                && !registry.HasTopAt(targetPos)
                && GhostPlacementRules.TryResolveCellSwap(
                    fromState,
                    ghost,
                    out var moverTo,
                    out var ghostFrom,
                    out var ghostTo)) {
                plan = new DiceSlidePlan(
                    fromState,
                    moverTo,
                    GhostLandingMode.CellSwap,
                    ghostFrom,
                    ghostTo);
                return true;
            }

            rejectReason = $"target={FormatGrid(targetPos)} occupied";
            return false;
        }

        static bool TryEvaluateTopSlide(
            DiceState fromState,
            Direction direction,
            DiceRegistry registry,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            var targetPos = fromState.GridPos + direction.ToGridDelta();
            if (registry.BlocksTraversalBetween(fromState.GridPos, targetPos)) {
                rejectReason = $"target={FormatGrid(targetPos)} blocked-by-partition";
                return false;
            }

            if (registry.TryGetBottomAt(targetPos, out var bottom)
                && !registry.HasTopAt(targetPos)
                && GhostPlacementRules.TryResolveInCellPromote(
                    fromState,
                    bottom,
                    out var promoteMoverTo,
                    out var promoteGhostFrom,
                    out var promoteGhostTo)) {
                plan = new DiceSlidePlan(
                    fromState,
                    promoteMoverTo,
                    GhostLandingMode.InCellPromoteGhost,
                    promoteGhostFrom,
                    promoteGhostTo);
                return true;
            }

            if (registry.CanAcceptTopDiceAt(targetPos)) {
                plan = new DiceSlidePlan(
                    fromState,
                    new DiceState(targetPos, fromState.Orientation, DiceStackTier.Top, fromState.Kind));
                return true;
            }

            if (registry.CanPlaceBottomDiceAt(targetPos)) {
                plan = new DiceSlidePlan(
                    fromState,
                    new DiceState(targetPos, fromState.Orientation, DiceStackTier.Bottom, fromState.Kind));
                return true;
            }

            if (registry.TryGetTopAt(targetPos, out var topGhost)
                && GhostPlacementRules.TryResolveCellSwap(
                    fromState,
                    topGhost,
                    out var topMoverTo,
                    out var topGhostFrom,
                    out var topGhostTo)) {
                plan = new DiceSlidePlan(
                    fromState,
                    topMoverTo,
                    GhostLandingMode.CellSwap,
                    topGhostFrom,
                    topGhostTo);
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
