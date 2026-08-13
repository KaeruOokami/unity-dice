using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Shared ghost swap / promote landing tree for slide and grid paths.
    /// Solid tier selection uses <see cref="SolidLandingTierResolver"/>.
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

            // Horizontal: same-tier ghost overlap → CellSwap to previous cell.
            if (registry.TryGetDiceAt(targetCell, fromTier, out var sameTierGhost)
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

            // Sink erasure ghost: still on grid for the player, but crushable by dice.
            // Treat as empty here; Place/MoveDice → EvictErasingDiceAt removes it.
            if (!registry.TryGetDiceAt(targetCell, landingTier, out var landingOccupant)
                || landingOccupant == null
                || GhostPlacementRules.IsCrushableByDicePlacement(landingOccupant)) {
                return true;
            }

            // Kind Ghost (幽霊ダイス) swap / promote paths below.
            var landingGhost = landingOccupant;

            // Vertical: Top demoting onto ghost Bottom → same-cell promote.
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

            // Ascent Bottom → Top ghost: diagonal swap.
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

            // Grid roll only: occupy Top by overwrite when ghost cannot swap.
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
