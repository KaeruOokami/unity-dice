using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Grid
{
    public static class PartitionBoundaryPolicy
    {
        public static bool BlocksMovement(
            VersusArenaLayout layout,
            Vector2Int fromCell,
            Vector2Int toCell,
            PlayerSlot? movementOwner)
        {
            if (layout == null)
            {
                return false;
            }

            if (layout.CrossesPartition(fromCell, toCell))
            {
                return true;
            }

            if (movementOwner.HasValue && !layout.IsInsidePlayerRegion(movementOwner.Value, toCell))
            {
                return true;
            }

            return false;
        }
    }
}
