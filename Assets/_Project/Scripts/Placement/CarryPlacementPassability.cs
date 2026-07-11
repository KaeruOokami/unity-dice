using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class CarryPlacementPassability
    {
        public static bool TryResolveTarget(
            Vector2Int targetGrid,
            IDicePlacement placement,
            out DiceStackTier targetTier,
            out string rejectReason) {
            targetTier = default;
            rejectReason = null;

            if (placement.CanPlaceBottomDiceAt(targetGrid)) {
                targetTier = DiceStackTier.Bottom;
                return true;
            }

            if (placement.CanPlaceTopDiceAt(targetGrid)) {
                targetTier = DiceStackTier.Top;
                return true;
            }

            if (placement.CanAcceptTopDiceAt(targetGrid)) {
                targetTier = DiceStackTier.Top;
                return true;
            }

            rejectReason = $"target={FormatGrid(targetGrid)} occupied";
            return false;
        }

        public static bool CanPlaceAt(
            Vector2Int targetGrid,
            DiceStackTier targetTier,
            IDicePlacement placement,
            out string rejectReason) {
            rejectReason = null;
            if (targetTier == DiceStackTier.Top) {
                if (!placement.CanPlaceTopDiceAt(targetGrid)
                    && !placement.CanAcceptTopDiceAt(targetGrid)) {
                    rejectReason = $"target={FormatGrid(targetGrid)} cannot-place-top";
                    return false;
                }

                return true;
            }

            if (!placement.CanPlaceBottomDiceAt(targetGrid)) {
                rejectReason = $"target={FormatGrid(targetGrid)} cannot-place-bottom";
                return false;
            }

            return true;
        }

        static string FormatGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }
    }
}
