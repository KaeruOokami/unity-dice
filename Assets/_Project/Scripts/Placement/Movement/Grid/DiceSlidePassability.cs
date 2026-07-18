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

            if (TryBuildLandingPlan(
                fromState,
                targetPos,
                DiceStackTier.Bottom,
                registry,
                out plan)) {
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

            if (TryBuildLandingPlan(
                fromState,
                targetPos,
                DiceStackTier.Top,
                registry,
                out plan)) {
                return true;
            }

            rejectReason = $"target={FormatGrid(targetPos)} blocked";
            return false;
        }

        /// <summary>
        /// Same rules as grid landing: ghosts invisible for solid place; same-slot ghost → swap.
        /// </summary>
        static bool TryBuildLandingPlan(
            DiceState fromState,
            Vector2Int targetPos,
            DiceStackTier fromTier,
            DiceRegistry registry,
            out DiceSlidePlan plan) {
            plan = default;

            if (GhostPlacementRules.IsPassThroughKind(fromState.Kind)) {
                return false;
            }

            if (registry.TryGetDiceAt(targetPos, fromTier, out var sameTierGhost)
                && GhostPlacementRules.TryResolveCellSwap(
                    fromState,
                    sameTierGhost,
                    out var sameTierMoverTo,
                    out var sameTierGhostFrom,
                    out var sameTierGhostTo)) {
                plan = new DiceSlidePlan(
                    fromState,
                    sameTierMoverTo,
                    GhostLandingMode.CellSwap,
                    sameTierGhostFrom,
                    sameTierGhostTo);
                return true;
            }

            if (!TryResolveSolidLandingTier(fromTier, targetPos, registry, out var landingTier)) {
                return false;
            }

            var moverTo = new DiceState(
                targetPos,
                fromState.Orientation,
                landingTier,
                fromState.Kind);

            if (!registry.TryGetDiceAt(targetPos, landingTier, out var landingGhost) || landingGhost == null) {
                plan = new DiceSlidePlan(fromState, moverTo);
                return true;
            }

            // Vertical demote onto ghost Bottom → in-cell promote.
            if (fromTier == DiceStackTier.Top
                && landingTier == DiceStackTier.Bottom
                && GhostPlacementRules.TryResolveInCellPromote(
                    fromState,
                    landingGhost,
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

            var landingProbe = new DiceState(
                fromState.GridPos,
                fromState.Orientation,
                landingTier,
                fromState.Kind);
            if (GhostPlacementRules.TryResolveCellSwap(
                landingProbe,
                landingGhost,
                out moverTo,
                out var landingGhostFrom,
                out var landingGhostTo)) {
                plan = new DiceSlidePlan(
                    fromState,
                    moverTo,
                    GhostLandingMode.CellSwap,
                    landingGhostFrom,
                    landingGhostTo);
                return true;
            }

            return false;
        }

        static bool TryResolveSolidLandingTier(
            DiceStackTier fromTier,
            Vector2Int cell,
            DiceRegistry registry,
            out DiceStackTier landingTier) {
            landingTier = default;

            if (fromTier == DiceStackTier.Bottom) {
                if (GhostPlacementRules.CanPlaceSolidBottomAt(registry, cell)) {
                    landingTier = DiceStackTier.Bottom;
                    return true;
                }

                if (GhostPlacementRules.CanPlaceSolidTopAt(registry, cell)) {
                    landingTier = DiceStackTier.Top;
                    return true;
                }

                return false;
            }

            if (GhostPlacementRules.CanPlaceSolidBottomAt(registry, cell)) {
                landingTier = DiceStackTier.Bottom;
                return true;
            }

            if (GhostPlacementRules.CanPlaceSolidTopAt(registry, cell)) {
                landingTier = DiceStackTier.Top;
                return true;
            }

            return false;
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

        static string FormatGrid(UnityEngine.Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
