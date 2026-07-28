using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// How to resolve player support at a cell.
    /// </summary>
    public enum PlayerSupportResolutionKind
    {
        /// <summary>Top solid → Bottom (optional pending) → Floor.</summary>
        HighestSolid = 0,

        /// <summary>Top solid → Bottom solid → Floor (no pending bottom).</summary>
        ElevatedSolid,

        /// <summary>Empty bottom slot → Floor; else Bottom solid → Floor.</summary>
        BottomOrFloor,
    }

    /// <summary>
    /// Single source for "what can the player stand on at this cell".
    /// </summary>
    public static class PlayerSupportQuery
    {
        public static void ResolveAt(
            Vector2Int cell,
            DiceRegistry registry,
            float floorSurfaceWorldY,
            out DiceController targetDice,
            out int targetLevel,
            out float targetSurfaceWorldY,
            bool includePendingBottom = true,
            DiceController excludeDice = null) {
            ResolveAt(
                cell,
                registry,
                floorSurfaceWorldY,
                PlayerSupportResolutionKind.HighestSolid,
                out targetDice,
                out targetLevel,
                out targetSurfaceWorldY,
                includePendingBottom,
                excludeDice);
        }

        public static void ResolveAt(
            Vector2Int cell,
            DiceRegistry registry,
            float floorSurfaceWorldY,
            PlayerSupportResolutionKind kind,
            out DiceController targetDice,
            out int targetLevel,
            out float targetSurfaceWorldY,
            bool includePendingBottom = true,
            DiceController excludeDice = null) {
            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;
            targetSurfaceWorldY = floorSurfaceWorldY;

            if (registry == null) {
                return;
            }

            switch (kind) {
                case PlayerSupportResolutionKind.BottomOrFloor:
                    ResolveBottomOrFloor(
                        cell,
                        registry,
                        floorSurfaceWorldY,
                        excludeDice,
                        out targetDice,
                        out targetLevel,
                        out targetSurfaceWorldY);
                    return;

                case PlayerSupportResolutionKind.ElevatedSolid:
                    ResolveElevatedSolid(
                        cell,
                        registry,
                        floorSurfaceWorldY,
                        excludeDice,
                        out targetDice,
                        out targetLevel,
                        out targetSurfaceWorldY);
                    return;

                default:
                    ResolveHighestSolid(
                        cell,
                        registry,
                        floorSurfaceWorldY,
                        includePendingBottom,
                        excludeDice,
                        out targetDice,
                        out targetLevel,
                        out targetSurfaceWorldY);
                    return;
            }
        }

        static void ResolveHighestSolid(
            Vector2Int cell,
            DiceRegistry registry,
            float floorSurfaceWorldY,
            bool includePendingBottom,
            DiceController excludeDice,
            out DiceController targetDice,
            out int targetLevel,
            out float targetSurfaceWorldY) {
            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;
            targetSurfaceWorldY = floorSurfaceWorldY;

            if (TryResolveTop(cell, registry, excludeDice, out targetDice, out targetSurfaceWorldY)) {
                targetLevel = SurfaceHeightLevel.Top;
                return;
            }

            if (includePendingBottom
                && registry.TryGetBottomIncludingPending(cell, out var bottomOrPending)
                && IsSolidSupport(bottomOrPending, excludeDice)) {
                targetDice = bottomOrPending;
                targetLevel = SurfaceHeightLevel.Bottom;
                targetSurfaceWorldY = bottomOrPending.GetLogicalTopSurfaceWorldY();
                return;
            }

            if (!includePendingBottom
                && registry.TryGetBottomAt(cell, out var bottom)
                && IsSolidSupport(bottom, excludeDice)) {
                targetDice = bottom;
                targetLevel = SurfaceHeightLevel.Bottom;
                targetSurfaceWorldY = bottom.GetLogicalTopSurfaceWorldY();
            }
        }

        static void ResolveElevatedSolid(
            Vector2Int cell,
            DiceRegistry registry,
            float floorSurfaceWorldY,
            DiceController excludeDice,
            out DiceController targetDice,
            out int targetLevel,
            out float targetSurfaceWorldY) {
            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;
            targetSurfaceWorldY = floorSurfaceWorldY;

            if (TryResolveTop(cell, registry, excludeDice, out targetDice, out targetSurfaceWorldY)) {
                targetLevel = SurfaceHeightLevel.Top;
                return;
            }

            if (registry.TryGetBottomAt(cell, out var bottom)
                && IsSolidSupport(bottom, excludeDice)) {
                targetDice = bottom;
                targetLevel = SurfaceHeightLevel.Bottom;
                targetSurfaceWorldY = bottom.GetLogicalTopSurfaceWorldY();
            }
        }

        static void ResolveBottomOrFloor(
            Vector2Int cell,
            DiceRegistry registry,
            float floorSurfaceWorldY,
            DiceController excludeDice,
            out DiceController targetDice,
            out int targetLevel,
            out float targetSurfaceWorldY) {
            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;
            targetSurfaceWorldY = floorSurfaceWorldY;

            if (registry.CanPlaceBottomDiceAt(cell)) {
                return;
            }

            if (registry.TryGetBottomAt(cell, out var bottom)
                && IsSolidSupport(bottom, excludeDice)) {
                targetDice = bottom;
                targetLevel = SurfaceHeightLevel.Bottom;
                targetSurfaceWorldY = bottom.GetLogicalTopSurfaceWorldY();
            }
        }

        static bool TryResolveTop(
            Vector2Int cell,
            DiceRegistry registry,
            DiceController excludeDice,
            out DiceController top,
            out float surfaceWorldY) {
            top = null;
            surfaceWorldY = 0f;
            if (!registry.TryGetTopAt(cell, out top) || !IsSolidSupport(top, excludeDice)) {
                top = null;
                return false;
            }

            surfaceWorldY = top.GetLogicalTopSurfaceWorldY();
            return true;
        }

        static bool IsSolidSupport(DiceController dice, DiceController excludeDice) {
            return dice != null
                && dice != excludeDice
                && !GhostPlacementRules.IsPlayerPassThrough(dice);
        }

        /// <summary>
        /// Die covering the player from above on the same cell:
        /// Floor → Bottom (pending included); Bottom → Top; Top → none.
        /// </summary>
        public static bool TryGetCoveringDice(
            Vector2Int cell,
            int playerLevel,
            DiceRegistry registry,
            out DiceController coveringDice,
            out int coveringLevel,
            DiceController excludeDice = null) {
            coveringDice = null;
            coveringLevel = SurfaceHeightLevel.Floor;

            if (registry == null) {
                return false;
            }

            if (SurfaceHeightLevel.IsFloor(playerLevel)) {
                if (!registry.TryGetBottomIncludingPending(cell, out var bottom)
                    || !IsSolidSupport(bottom, excludeDice)) {
                    return false;
                }

                coveringDice = bottom;
                coveringLevel = SurfaceHeightLevel.Bottom;
                return true;
            }

            if (playerLevel == SurfaceHeightLevel.Bottom) {
                if (!TryResolveTop(cell, registry, excludeDice, out var top, out _)) {
                    return false;
                }

                coveringDice = top;
                coveringLevel = SurfaceHeightLevel.Top;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves support then applies mount-block filtering (motion-follow, etc.).
        /// Elevated: blocked Top falls back to Bottom, then Floor.
        /// </summary>
        public static void ResolveMountableAt(
            Vector2Int cell,
            DiceRegistry registry,
            float floorSurfaceWorldY,
            PlayerSupportResolutionKind kind,
            System.Func<DiceController, bool> isMountBlocked,
            out DiceController targetDice,
            out int targetLevel,
            out float targetSurfaceWorldY,
            bool includePendingBottom = true,
            DiceController excludeDice = null) {
            ResolveAt(
                cell,
                registry,
                floorSurfaceWorldY,
                kind,
                out targetDice,
                out targetLevel,
                out targetSurfaceWorldY,
                includePendingBottom,
                excludeDice);

            if (targetDice == null || isMountBlocked == null || !isMountBlocked(targetDice)) {
                return;
            }

            if (kind == PlayerSupportResolutionKind.ElevatedSolid
                && targetLevel >= SurfaceHeightLevel.Top
                && registry.TryGetBottomAt(cell, out var bottom)
                && IsSolidSupport(bottom, excludeDice)
                && !isMountBlocked(bottom)) {
                targetDice = bottom;
                targetLevel = SurfaceHeightLevel.Bottom;
                targetSurfaceWorldY = bottom.GetLogicalTopSurfaceWorldY();
                return;
            }

            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;
            targetSurfaceWorldY = floorSurfaceWorldY;
        }
    }
}
