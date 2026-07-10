using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public sealed class CellOccupancyQuery
    {
        readonly Board board;
        readonly IDicePlacement placement;

        public CellOccupancyQuery(Board board, IDicePlacement placement) {
            this.board = board;
            this.placement = placement;
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

        public bool TryResolveLandingTier(
            DiceStackTier fromTier,
            Vector2Int cell,
            out DiceStackTier landingTier) {
            landingTier = default;
            if (!IsPassableCell(cell)) {
                return false;
            }

            if (fromTier == DiceStackTier.Bottom) {
                if (placement.CanPlaceBottomDiceAt(cell)) {
                    landingTier = DiceStackTier.Bottom;
                    return true;
                }

                if (placement.CanPlaceTopDiceAt(cell)) {
                    landingTier = DiceStackTier.Top;
                    return true;
                }

                return false;
            }

            if (placement.CanPlaceBottomDiceAt(cell)) {
                landingTier = DiceStackTier.Bottom;
                return true;
            }

            if (placement.CanPlaceTopDiceAt(cell)) {
                landingTier = DiceStackTier.Top;
                return true;
            }

            return false;
        }
    }
}
