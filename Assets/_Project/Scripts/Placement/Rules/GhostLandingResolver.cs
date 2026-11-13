using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Resolves ghost landing as a final cell occupancy solution.
    /// Solutions are selected by occupancy preconditions, not by first-match branch order.
    /// </summary>
    public static class GhostLandingResolver
    {
        public static bool TryResolve(
            DiceState moverFrom,
            Vector2Int targetCell,
            DiceRegistry registry,
            SolidLandingStackPolicy stackPolicy,
            System.Func<Vector2Int, bool> canOverwriteTopAt,
            out DiceStackTier landingTier,
            out GhostLandingMode ghostLanding,
            out DiceState moverTo,
            out DiceState ghostFrom,
            out DiceState ghostTo) {
            landingTier = default;
            ghostLanding = GhostLandingMode.None;
            moverTo = default;
            ghostFrom = default;
            ghostTo = default;

            if (registry == null || GhostPlacementRules.IsPassThroughKind(moverFrom.Kind)) {
                return false;
            }

            var fromTier = moverFrom.Tier;
            var hasSameTierGhost = registry.TryGetDiceAt(targetCell, fromTier, out var sameTierGhost)
                && sameTierGhost != null
                && GhostPlacementRules.AllowsDiceSwapThrough(sameTierGhost);
            var hasTopGhost = registry.TryGetTopAt(targetCell, out var topGhost)
                && topGhost != null
                && GhostPlacementRules.AllowsDiceSwapThrough(topGhost);
            var hasBottomGhost = registry.TryGetBottomAt(targetCell, out var bottomGhost)
                && bottomGhost != null
                && GhostPlacementRules.AllowsDiceSwapThrough(bottomGhost);

            // Final occupancy: Top entry into ghost Bottom + ghost Top.
            // mover → Bottom, bottom ghost → Top, top ghost → mover previous cell.
            if (fromTier == DiceStackTier.Top
                && hasTopGhost
                && hasBottomGhost
                && GhostPlacementRules.TryResolveGhostStackTopEntry(
                    moverFrom,
                    topGhost,
                    bottomGhost,
                    out moverTo,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = DiceStackTier.Bottom;
                ghostLanding = GhostLandingMode.CellSwapWithInCellPromote;
                return true;
            }

            // Same-tier swap when the target slot is only a same-tier ghost
            // (not the dual-ghost Top-entry case above).
            if (hasSameTierGhost
                && !(fromTier == DiceStackTier.Top && hasBottomGhost)
                && GhostPlacementRules.TryResolveCellSwap(
                    moverFrom,
                    sameTierGhost,
                    out moverTo,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = fromTier;
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            // Ascent: Bottom mover → Top ghost (Top slot claim becomes mover).
            if (fromTier == DiceStackTier.Bottom
                && stackPolicy == SolidLandingStackPolicy.GridRoll
                && hasTopGhost
                && GhostPlacementRules.TryResolveAscentGhostSwap(
                    moverFrom,
                    topGhost,
                    out moverTo,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = DiceStackTier.Top;
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            // In-cell promote: Top mover onto ghost Bottom with Top slot free.
            if (fromTier == DiceStackTier.Top
                && hasBottomGhost
                && !hasTopGhost
                && !registry.HasTopSlotClaimAt(targetCell)
                && GhostPlacementRules.TryResolveInCellPromote(
                    moverFrom,
                    bottomGhost,
                    out moverTo,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = DiceStackTier.Bottom;
                ghostLanding = GhostLandingMode.InCellPromoteGhost;
                return true;
            }

            if (!SolidLandingTierResolver.TryResolve(
                registry,
                fromTier,
                targetCell,
                stackPolicy,
                canOverwriteTopAt,
                out landingTier)) {
                return false;
            }

            moverTo = new DiceState(
                targetCell,
                moverFrom.Orientation,
                landingTier,
                moverFrom.Kind);

            // Sink erasure ghost: crushable overwrite.
            if (!registry.TryGetDiceAt(targetCell, landingTier, out var landingOccupant)
                || landingOccupant == null
                || GhostPlacementRules.IsCrushableByDicePlacement(landingOccupant)) {
                return true;
            }

            var landingGhost = landingOccupant;

            if (fromTier == DiceStackTier.Top
                && landingTier == DiceStackTier.Bottom
                && GhostPlacementRules.TryResolveInCellPromote(
                    moverFrom,
                    landingGhost,
                    out moverTo,
                    out ghostFrom,
                    out ghostTo)) {
                ghostLanding = GhostLandingMode.InCellPromoteGhost;
                return true;
            }

            if (fromTier == DiceStackTier.Bottom
                && landingTier == DiceStackTier.Top
                && GhostPlacementRules.TryResolveAscentGhostSwap(
                    moverFrom,
                    landingGhost,
                    out moverTo,
                    out ghostFrom,
                    out ghostTo)) {
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            var landingProbe = new DiceState(
                moverFrom.GridPos,
                moverFrom.Orientation,
                landingTier,
                moverFrom.Kind);
            if (GhostPlacementRules.TryResolveCellSwap(
                landingProbe,
                landingGhost,
                out moverTo,
                out ghostFrom,
                out ghostTo)) {
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            if (stackPolicy == SolidLandingStackPolicy.GridRoll
                && landingTier == DiceStackTier.Top
                && canOverwriteTopAt != null
                && canOverwriteTopAt(targetCell)) {
                moverTo = new DiceState(
                    targetCell,
                    moverFrom.Orientation,
                    landingTier,
                    moverFrom.Kind);
                ghostLanding = GhostLandingMode.None;
                ghostFrom = default;
                ghostTo = default;
                return true;
            }

            landingTier = default;
            moverTo = default;
            return false;
        }
    }
}
