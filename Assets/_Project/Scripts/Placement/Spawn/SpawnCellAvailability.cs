using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Spawn lottery reads board occupancy only through this view:
    /// committed slots, in-flight spawn claims, and erasure blockers.
    /// One in-flight spawn claims the whole cell (no second dice onto the same cell).
    /// </summary>
    public static class SpawnCellAvailability
    {
        public static bool CanSpawnBottomAt(DiceRegistry registry, Vector2Int cell) {
            if (registry == null || !IsPassableFloor(registry, cell)) {
                return false;
            }

            if (HasSpawnBlockerAt(registry, cell)) {
                return false;
            }

            return registry.CanPlaceBottomDiceAt(cell);
        }

        public static bool CanSpawnTopAt(DiceRegistry registry, Vector2Int cell) {
            if (registry == null || !IsPassableFloor(registry, cell)) {
                return false;
            }

            if (HasSpawnBlockerAt(registry, cell)) {
                return false;
            }

            return registry.CanPlaceTopDiceAt(cell);
        }

        /// <summary>
        /// Cell cannot accept a new spawn: erasure / sink ghosts, or an in-flight spawn claim.
        /// </summary>
        public static bool HasSpawnBlockerAt(DiceRegistry registry, Vector2Int cell) {
            if (registry == null) {
                return true;
            }

            if (registry.HasErasingDiceAt(cell)) {
                return true;
            }

            return registry.HasInFlightSpawnAt(cell);
        }

        static bool IsPassableFloor(DiceRegistry registry, Vector2Int cell) {
            var board = registry.Board;
            return board != null
                && board.IsInside(cell)
                && board.GetCell(cell) == CellType.Floor;
        }
    }
}
