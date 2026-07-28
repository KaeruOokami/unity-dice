using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

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
            if (BlocksSlideTraversal(fromState, targetPos, registry)) {
                rejectReason = $"target={FormatGrid(targetPos)} blocked-by-partition";
                return false;
            }

            if (TryBuildLandingPlan(fromState, targetPos, registry, out plan)) {
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
            if (BlocksSlideTraversal(fromState, targetPos, registry)) {
                rejectReason = $"target={FormatGrid(targetPos)} blocked-by-partition";
                return false;
            }

            if (TryBuildLandingPlan(fromState, targetPos, registry, out plan)) {
                return true;
            }

            rejectReason = $"target={FormatGrid(targetPos)} blocked";
            return false;
        }

        static bool TryBuildLandingPlan(
            DiceState fromState,
            Vector2Int targetPos,
            DiceRegistry registry,
            out DiceSlidePlan plan) {
            plan = default;
            if (!GhostLandingResolver.TryResolve(
                fromState,
                targetPos,
                registry,
                SolidLandingStackPolicy.Slide,
                canOverwriteTopAt: null,
                out _,
                out var ghostLanding,
                out var moverTo,
                out var ghostFrom,
                out var ghostTo)) {
                return false;
            }

            plan = ghostLanding == GhostLandingMode.None
                ? new DiceSlidePlan(fromState, moverTo)
                : new DiceSlidePlan(fromState, moverTo, ghostLanding, ghostFrom, ghostTo);
            return true;
        }

        static bool BlocksSlideTraversal(
            DiceState fromState,
            Vector2Int targetPos,
            DiceRegistry registry) {
            if (DiceBehaviorResolver.GetBehavior(fromState.Kind).Capabilities.IgnoresPartitionBoundary) {
                return false;
            }

            return registry.BlocksTraversalBetween(fromState.GridPos, targetPos);
        }

        static string FormatGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
