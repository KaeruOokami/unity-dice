using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Parallel walk on a multi-cell footprint (jumbo 2×2). Same die, same surface height.
    /// </summary>
    public static class ExpandedFootprintWalkPolicy
    {
        public static bool IsWithinFootprint(DiceController footprintDice, Vector2Int cell) {
            if (footprintDice == null || !footprintDice.Capabilities.HasExpandedFootprint) {
                return false;
            }

            return JumboFootprint.Contains(footprintDice.CurrentState.GridPos, cell);
        }

        public static bool IsIntraFootprintMove(
            DiceController footprintDice,
            Vector2Int fromCell,
            Vector2Int toCell) {
            return IsWithinFootprint(footprintDice, fromCell)
                && IsWithinFootprint(footprintDice, toCell);
        }

        public static DiceController ResolveFootprintDice(DiceController standingDice, DiceRegistry registry) {
            if (standingDice == null) {
                return null;
            }

            if (standingDice.Capabilities.HasExpandedFootprint) {
                return standingDice;
            }

            return registry != null ? registry.ResolveSupportBottom(standingDice) : standingDice;
        }

        /// <summary>
        /// Standing level on an expanded footprint: Top while Top occupancy is kept, else Bottom.
        /// </summary>
        public static int ResolveStandingLevel(DiceController footprintDice, int fromLevel) {
            if (footprintDice == null || !footprintDice.Capabilities.HasExpandedFootprint) {
                return fromLevel;
            }

            return footprintDice.KeepsJumboTopOccupancy
                ? SurfaceHeightLevel.Top
                : SurfaceHeightLevel.Bottom;
        }

        public static DiceStackTier ResolveStandingTier(DiceController dice) {
            if (dice == null) {
                return DiceStackTier.Bottom;
            }

            return ResolveStandingLevel(dice, SurfaceHeightLevel.Bottom) >= SurfaceHeightLevel.Top
                ? DiceStackTier.Top
                : DiceStackTier.Bottom;
        }

        public static bool TryEvaluateParallelWalk(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            DiceController standingDice,
            DiceRegistry registry,
            out MovementTransition transition) {
            transition = default;
            var footprintDice = ResolveFootprintDice(standingDice, registry);
            if (footprintDice == null
                || !footprintDice.Capabilities.HasExpandedFootprint
                || !IsIntraFootprintMove(footprintDice, fromCell, toCell)) {
                return false;
            }

            if (!IsSupportedAtCell(footprintDice, toCell, fromLevel, registry)) {
                return false;
            }

            var level = ResolveStandingLevel(footprintDice, fromLevel);
            transition = MovementTransition.Walkable(
                footprintDice,
                level,
                MovementTransitionRoute.HeightTransfer);
            return true;
        }

        static bool IsSupportedAtCell(
            DiceController footprintDice,
            Vector2Int cell,
            int fromLevel,
            DiceRegistry registry) {
            if (registry == null) {
                return false;
            }

            if (SurfaceHeightLevel.IsAtOrAboveTop(fromLevel)
                || ResolveStandingLevel(footprintDice, fromLevel) >= SurfaceHeightLevel.Top) {
                return registry.TryGetTopAt(cell, out var top)
                    && top == footprintDice
                    && !GhostPlacementRules.IsPlayerPassThrough(top);
            }

            return registry.TryGetBottomAt(cell, out var bottom)
                && bottom == footprintDice
                && !GhostPlacementRules.IsPlayerPassThrough(bottom);
        }

        public static bool OccupiesCell(DiceController dice, Vector2Int cell) {
            if (dice == null) {
                return false;
            }

            if (!dice.Capabilities.HasExpandedFootprint) {
                return dice.CurrentState.GridPos == cell;
            }

            return JumboFootprint.Contains(dice.CurrentState.GridPos, cell);
        }

        public static Vector2 GetFootprintCenterXZ(DiceController footprintDice) {
            if (footprintDice == null) {
                return Vector2.zero;
            }

            var center = footprintDice.GetLogicalCenterWorld();
            return new Vector2(center.x, center.z);
        }

        public static float GetFootprintWalkHalfExtent(float cellSize) {
            return cellSize * JumboFootprint.Size * 0.5f;
        }
    }
}
