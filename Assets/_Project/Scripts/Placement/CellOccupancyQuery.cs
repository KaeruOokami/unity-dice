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

        public bool TryGetOccupancyTier(Vector2Int cell, out CellOccupancyTier tier) {
            tier = CellOccupancyTier.Invalid;
            if (!IsPassableCell(cell)) {
                return false;
            }

            if (placement.HasTopAt(cell)) {
                tier = CellOccupancyTier.Top;
                return true;
            }

            if (placement.HasBottomAt(cell)) {
                tier = CellOccupancyTier.Bottom;
                return true;
            }

            tier = CellOccupancyTier.Floor;
            return true;
        }

        public bool CanOverwriteTopAt(Vector2Int cell) {
            return IsPassableCell(cell)
                && placement.CanAcceptTopDiceAt(cell)
                && !placement.CanPlaceTopDiceAt(cell);
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

            if (TryResolveGhostLanding(
                fromTier,
                fromCell,
                cell,
                moverKind,
                out landingTier,
                out ghostLanding,
                out ghostFrom,
                out ghostTo)) {
                return true;
            }

            if (fromTier == DiceStackTier.Bottom) {
                if (placement.CanPlaceBottomDiceAt(cell)) {
                    landingTier = DiceStackTier.Bottom;
                    return true;
                }

                if (placement.CanPlaceTopDiceAt(cell) || CanOverwriteTopAt(cell)) {
                    landingTier = DiceStackTier.Top;
                    return true;
                }

                return false;
            }

            if (placement.CanPlaceBottomDiceAt(cell)) {
                landingTier = DiceStackTier.Bottom;
                return true;
            }

            if (placement.CanPlaceTopDiceAt(cell) || CanOverwriteTopAt(cell)) {
                landingTier = DiceStackTier.Top;
                return true;
            }

            return false;
        }

        bool TryResolveGhostLanding(
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

            if (registry == null || GhostPlacementRules.IsPassThroughKind(moverKind)) {
                return false;
            }

            var probe = new DiceState(fromCell, DiceOrientation.Default, fromTier, moverKind);

            if (fromTier == DiceStackTier.Bottom
                && registry.TryGetBottomAt(cell, out var ghostBottom)
                && !registry.HasTopAt(cell)
                && GhostPlacementRules.TryResolveCellSwap(
                    probe,
                    ghostBottom,
                    out _,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = DiceStackTier.Bottom;
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            // Only Top arrivals (or explicit top-of-ghost placement) promote ghost in-cell.
            // Bottom arrivals onto ghost use CellSwap above, never silent promote.
            if (fromTier == DiceStackTier.Top
                && registry.TryGetBottomAt(cell, out var promoteGhost)
                && !registry.HasTopAt(cell)
                && GhostPlacementRules.TryResolveInCellPromote(
                    probe,
                    promoteGhost,
                    out _,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = DiceStackTier.Bottom;
                ghostLanding = GhostLandingMode.InCellPromoteGhost;
                return true;
            }

            if (fromTier == DiceStackTier.Top
                && registry.TryGetTopAt(cell, out var topGhost)
                && GhostPlacementRules.TryResolveCellSwap(
                    probe,
                    topGhost,
                    out _,
                    out ghostFrom,
                    out ghostTo)) {
                landingTier = DiceStackTier.Top;
                ghostLanding = GhostLandingMode.CellSwap;
                return true;
            }

            return false;
        }
    }
}
