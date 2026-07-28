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
            PlayerSupportQuery.ResolveAt(
                cell,
                registry,
                floorSurfaceWorldY: 0f,
                out var targetDice,
                out var targetLevel,
                out _,
                includePendingBottom: false,
                excludeDice);

            if (targetDice == null || targetLevel == SurfaceHeightLevel.Floor) {
                return CharacterSupportState.OnFloor(cell);
            }

            if (targetLevel >= SurfaceHeightLevel.Top) {
                return CharacterSupportState.OnDice(
                    cell,
                    SurfaceHeightLevel.Top,
                    SupportRef.DiceSupport(targetDice, DiceSurfaceLevel.Top));
            }

            var level = ExpandedFootprintWalkPolicy.ResolveStandingLevel(
                targetDice,
                SurfaceHeightLevel.Bottom);
            var surfaceLevel = level >= SurfaceHeightLevel.Top
                ? DiceSurfaceLevel.Top
                : DiceSurfaceLevel.Bottom;
            return CharacterSupportState.OnDice(
                cell,
                level,
                SupportRef.DiceSupport(targetDice, surfaceLevel));
        }

        public static float ResolveSurfaceWorldY(
            Vector2Int cell,
            DiceRegistry registry,
            Board board,
            DiceController excludeDice = null) {
            var floorY = board != null ? board.FloorSurfaceWorldY : 0f;
            PlayerSupportQuery.ResolveAt(
                cell,
                registry,
                floorY,
                out _,
                out _,
                out var surfaceY,
                includePendingBottom: false,
                excludeDice);
            return surfaceY;
        }
    }
}
