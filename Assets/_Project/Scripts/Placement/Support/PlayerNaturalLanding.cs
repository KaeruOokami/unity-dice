using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement.Support;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Shared player landing: drop onto the highest solid die at a cell, else the floor.
    /// </summary>
    public static class PlayerNaturalLanding
    {
        public static CharacterSupportState Resolve(
            Vector2Int cell,
            DiceRegistry registry,
            DiceController excludeDice = null) {
            if (registry != null
                && registry.TryGetTopAt(cell, out var top)
                && top != null
                && top != excludeDice
                && !GhostPlacementRules.IsPlayerPassThrough(top)) {
                return CharacterSupportState.OnDice(
                    cell,
                    SurfaceHeightLevel.Top,
                    SupportRef.DiceSupport(top, DiceSurfaceLevel.Top));
            }

            if (registry != null
                && registry.TryGetBottomAt(cell, out var bottom)
                && bottom != null
                && bottom != excludeDice
                && !GhostPlacementRules.IsPlayerPassThrough(bottom)) {
                return CharacterSupportState.OnDice(
                    cell,
                    SurfaceHeightLevel.Bottom,
                    SupportRef.DiceSupport(bottom, DiceSurfaceLevel.Bottom));
            }

            return CharacterSupportState.OnFloor(cell);
        }

        public static float ResolveSurfaceWorldY(
            Vector2Int cell,
            DiceRegistry registry,
            Board board,
            DiceController excludeDice = null) {
            if (registry != null
                && registry.TryGetTopAt(cell, out var top)
                && top != null
                && top != excludeDice
                && !GhostPlacementRules.IsPlayerPassThrough(top)) {
                return top.GetLogicalTopSurfaceWorldY();
            }

            if (registry != null
                && registry.TryGetBottomAt(cell, out var bottom)
                && bottom != null
                && bottom != excludeDice
                && !GhostPlacementRules.IsPlayerPassThrough(bottom)) {
                return bottom.GetLogicalTopSurfaceWorldY();
            }

            return board != null ? board.FloorSurfaceWorldY : 0f;
        }
    }
}
