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
        /// Pending Top spawn reserves Top for dice landing (same as occupied Top).
        /// </summary>
        public bool TryGetOccupancyTier(Vector2Int cell, out CellOccupancyTier tier) {
            tier = CellOccupancyTier.Invalid;
            if (!IsPassableCell(cell)) {
                return false;
            }

            if (GhostPlacementRules.HasSolidTopAt(registry, cell)
                || (registry != null && registry.HasPendingTopAt(cell))) {
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
            if (registry != null && registry.HasPendingTopAt(cell)) {
                return false;
            }

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
                return SolidLandingTierResolver.TryResolve(
                    registry,
                    fromTier,
                    cell,
                    SolidLandingStackPolicy.GridRoll,
                    CanOverwriteTopAt,
                    out landingTier);
            }

            var moverFrom = new DiceState(fromCell, DiceOrientation.Default, fromTier, moverKind);
            return GhostLandingResolver.TryResolve(
                moverFrom,
                cell,
                registry,
                SolidLandingStackPolicy.GridRoll,
                CanOverwriteTopAt,
                out landingTier,
                out ghostLanding,
                out _,
                out ghostFrom,
                out ghostTo);
        }
    }
}
