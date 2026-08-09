using DiceGame.Core;
using DiceGame.SimShared.Lift;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class CarryPlacementPassability
    {
        public static bool TryResolveTarget(
            Vector2Int targetGrid,
            IDicePlacement placement,
            out DiceStackTier targetTier,
            out string rejectReason)
        {
            targetTier = default;
            rejectReason = null;
            if (placement == null)
            {
                rejectReason = "no-placement";
                return false;
            }

            if (!CarryPlacementRules.TryResolveTarget(
                    targetGrid.x,
                    targetGrid.y,
                    (x, y) => placement.CanPlaceBottomDiceAt(new Vector2Int(x, y)),
                    (x, y) => placement.CanPlaceTopDiceAt(new Vector2Int(x, y)),
                    (x, y) => placement.CanAcceptTopDiceAt(new Vector2Int(x, y)),
                    out var tierNorm))
            {
                rejectReason = $"target={FormatGrid(targetGrid)} occupied";
                return false;
            }

            targetTier = tierNorm == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            return true;
        }

        public static bool CanPlaceAt(
            Vector2Int targetGrid,
            DiceStackTier targetTier,
            IDicePlacement placement,
            out string rejectReason)
        {
            rejectReason = null;
            if (targetTier == DiceStackTier.Top)
            {
                if (!placement.CanPlaceTopDiceAt(targetGrid)
                    && !placement.CanAcceptTopDiceAt(targetGrid))
                {
                    rejectReason = $"target={FormatGrid(targetGrid)} cannot-place-top";
                    return false;
                }

                return true;
            }

            if (!placement.CanPlaceBottomDiceAt(targetGrid))
            {
                rejectReason = $"target={FormatGrid(targetGrid)} cannot-place-bottom";
                return false;
            }

            return true;
        }

        static string FormatGrid(Vector2Int grid)
        {
            return $"({grid.x},{grid.y})";
        }
    }
}
