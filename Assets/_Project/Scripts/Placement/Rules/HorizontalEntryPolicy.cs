using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Horizontal walk entry: only the slot one tier above the player's standing level blocks.
    /// Floor → Bottom must be clear; Bottom → Top must be clear; Top has no slot above.
    /// Upper-tier dice (e.g. radiance Top over empty Bottom) do not block floor entry.
    /// </summary>
    public static class HorizontalEntryPolicy
    {
        public static bool IsClearForHorizontalEntry(
            int fromLevel,
            DiceRegistry registry,
            Vector2Int cell) {
            if (registry == null) {
                return false;
            }

            if (SurfaceHeightLevel.IsFloor(fromLevel)) {
                return IsBottomSlotClearForFloorEntry(registry, cell);
            }

            if (fromLevel == SurfaceHeightLevel.Bottom) {
                return !GhostPlacementRules.HasSolidTopAt(registry, cell);
            }

            // Top: no tier above in this stack model.
            return true;
        }

        /// <summary>
        /// Floor walk may enter a cell when Bottom (including pending) is not solid.
        /// Top occupancy is ignored — the player remains on the floor under that Top.
        /// </summary>
        public static bool IsBottomSlotClearForFloorEntry(DiceRegistry registry, Vector2Int cell) {
            if (registry == null) {
                return false;
            }

            if (GhostPlacementRules.HasSolidBottomAt(registry, cell)) {
                return false;
            }

            return !(registry.TryGetPendingBottomAt(cell, out var pending)
                && pending != null
                && !GhostPlacementRules.IsPlayerPassThrough(pending));
        }
    }
}
