using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public sealed class CellOccupancyQuery
    {
        readonly Board board;
        readonly IDicePlacement placement;
        readonly DiceRegistry registry;

        public CellOccupancyQuery(Board board, IDicePlacement placement) {
            this.board = board;
            this.placement = placement;
            registry = placement as DiceRegistry;
        }

        public static int ToTierRank(DiceStackTier tier) {
            return tier == DiceStackTier.Top ? (int)CellOccupancyTier.Top : (int)CellOccupancyTier.Bottom;
        }

        public static DiceStackTier ToDiceStackTier(int tierRank) {
            return tierRank >= (int)CellOccupancyTier.Top ? DiceStackTier.Top : DiceStackTier.Bottom;
        }

        public bool IsPassableCell(Vector2Int cell) {
            return board != null
                && board.IsInside(cell)
                && board.GetCell(cell) == CellType.Floor;
        }

        public bool BlocksRollBetween(Vector2Int fromCell, Vector2Int toCell) {
            return board != null && board.BlocksMovement(fromCell, toCell, null);
        }

        /// <summary>
        /// Occupancy for roll path ranks: pass-through dice do not count as solid.
        /// </summary>
        public bool TryGetOccupancyTier(Vector2Int cell, out CellOccupancyTier tier) {
            tier = CellOccupancyTier.Invalid;
            if (!IsPassableCell(cell)) {
                return false;
            }

            if (GhostPlacementRules.HasSolidTopAt(registry, cell)) {
                tier = CellOccupancyTier.Top;
                return true;
            }

            if (GhostPlacementRules.HasSolidBottomAt(registry, cell)) {
                tier = CellOccupancyTier.Bottom;
                return true;
            }

            tier = CellOccupancyTier.Floor;
            return true;
        }

        public bool CanOverwriteTopAt(Vector2Int cell) {
            return IsPassableCell(cell)
                && placement.CanAcceptTopDiceAt(cell)
                && !GhostPlacementRules.CanPlaceSolidTopAt(registry, cell);
        }

        public bool TryResolveLandingTier(
            DiceStackTier fromTier,
            Vector2Int fromCell,
            Vector2Int cell,
            DiceKind moverKind,
            out DiceStackTier landingTier,
            out GhostLandingMode ghostLanding,
            out DiceState ghostFrom,
            out DiceState ghostTo) {
            landingTier = default;
            ghostLanding = GhostLandingMode.None;
            ghostFrom = default;
            ghostTo = default;

            if (!IsPassableCell(cell)) {
                return false;
            }

            if (registry == null || GhostPlacementRules.IsPassThroughKind(moverKind)) {
                return TryResolveSolidLandingOnly(fromTier, cell, out landingTier);
            }

            // Horizontal: same-tier ghost overlap → CellSwap to previous cell.
            var sameTierProbe = new DiceState(fromCell, DiceOrientation.Default, fromTier, moverKind);
            if (registry.TryGetDiceAt(cell, fromTier, out var sameTierGhost)
                && GhostPlacementRules.TryResolveCellSwap(
                    sameTierProbe,
                    sameTierGhost,
                    out _,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = fromTier;
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            // Ghosts are invisible for solid occupancy.
            if (!TryResolveSolidLandingOnly(fromTier, cell, out landingTier)) {
                return false;
            }

            if (!registry.TryGetDiceAt(cell, landingTier, out var landingGhost) || landingGhost == null) {
                return true;
            }

            // Vertical: Top demoting onto ghost Bottom → same-cell promote (not previous-cell swap).
            if (fromTier == DiceStackTier.Top
                && landingTier == DiceStackTier.Bottom
                && GhostPlacementRules.TryResolveInCellPromote(
                    sameTierProbe,
                    landingGhost,
                    out _,
                    out ghostFrom,
                    out ghostTo)) {
                ghostLanding = GhostLandingMode.InCellPromoteGhost;
                return true;
            }

            // Horizontal (or stack): overlap landing slot ghost → CellSwap.
            var landingProbe = new DiceState(fromCell, DiceOrientation.Default, landingTier, moverKind);
            if (GhostPlacementRules.TryResolveCellSwap(
                landingProbe,
                landingGhost,
                out _,
                out ghostFrom,
                out ghostTo)) {
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            if (landingTier == DiceStackTier.Top && CanOverwriteTopAt(cell)) {
                return true;
            }

            landingTier = default;
            return false;
        }

        bool TryResolveSolidLandingOnly(
            DiceStackTier fromTier,
            Vector2Int cell,
            out DiceStackTier landingTier) {
            landingTier = default;

            if (fromTier == DiceStackTier.Bottom) {
                if (GhostPlacementRules.CanPlaceSolidBottomAt(registry, cell)) {
                    landingTier = DiceStackTier.Bottom;
                    return true;
                }

                if (GhostPlacementRules.CanPlaceSolidTopAt(registry, cell) || CanOverwriteTopAt(cell)) {
                    landingTier = DiceStackTier.Top;
                    return true;
                }

                return false;
            }

            if (GhostPlacementRules.CanPlaceSolidBottomAt(registry, cell)) {
                landingTier = DiceStackTier.Bottom;
                return true;
            }

            if (GhostPlacementRules.CanPlaceSolidTopAt(registry, cell) || CanOverwriteTopAt(cell)) {
                landingTier = DiceStackTier.Top;
                return true;
            }

            return false;
        }
    }
}
