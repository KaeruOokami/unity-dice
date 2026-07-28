using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Bottom→Top stack and top-overwrite rules for solid landing tier resolution.
    /// </summary>
    public enum SolidLandingStackPolicy
    {
        /// <summary>Ice slide: Bottom stays Bottom; no top overwrite.</summary>
        Slide = 0,

        /// <summary>Grid roll: Bottom may stack Top; top overwrite when allowed.</summary>
        GridRoll = 1,
    }

    /// <summary>
    /// Shared solid landing tier for slide and grid paths.
    /// Ghost swap/promote: <see cref="GhostLandingResolver"/>.
    /// </summary>
    public static class SolidLandingTierResolver
    {
        public static bool TryResolve(
            DiceRegistry registry,
            DiceStackTier fromTier,
            Vector2Int cell,
            SolidLandingStackPolicy stackPolicy,
            out DiceStackTier landingTier) {
            return TryResolve(registry, fromTier, cell, stackPolicy, canOverwriteTopAt: null, out landingTier);
        }

        public static bool TryResolve(
            DiceRegistry registry,
            DiceStackTier fromTier,
            Vector2Int cell,
            SolidLandingStackPolicy stackPolicy,
            System.Func<Vector2Int, bool> canOverwriteTopAt,
            out DiceStackTier landingTier) {
            landingTier = default;
            if (registry == null) {
                return false;
            }

            var allowBottomAscent = stackPolicy == SolidLandingStackPolicy.GridRoll;
            var allowTopOverwrite = stackPolicy == SolidLandingStackPolicy.GridRoll && canOverwriteTopAt != null;

            if (fromTier == DiceStackTier.Bottom) {
                if (GhostPlacementRules.CanPlaceSolidBottomAt(registry, cell)) {
                    landingTier = DiceStackTier.Bottom;
                    return true;
                }

                if (allowBottomAscent && TryResolveTopSlot(registry, cell, allowTopOverwrite, canOverwriteTopAt, out landingTier)) {
                    return true;
                }

                return false;
            }

            if (GhostPlacementRules.CanPlaceSolidBottomAt(registry, cell)) {
                landingTier = DiceStackTier.Bottom;
                return true;
            }

            if (TryResolveTopSlot(registry, cell, allowTopOverwrite, canOverwriteTopAt, out landingTier)) {
                return true;
            }

            return false;
        }

        static bool TryResolveTopSlot(
            DiceRegistry registry,
            Vector2Int cell,
            bool allowTopOverwrite,
            System.Func<Vector2Int, bool> canOverwriteTopAt,
            out DiceStackTier landingTier) {
            landingTier = DiceStackTier.Top;
            if (GhostPlacementRules.CanPlaceSolidTopAt(registry, cell)) {
                return true;
            }

            return allowTopOverwrite && canOverwriteTopAt != null && canOverwriteTopAt(cell);
        }
    }
}
