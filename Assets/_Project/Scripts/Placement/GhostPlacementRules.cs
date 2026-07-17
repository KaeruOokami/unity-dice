using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class GhostPlacementRules
    {
        public static bool IsPlayerPassThrough(DiceController dice) {
            // Sink-erasing Ghost is solid like a normal die until erasure completes.
            return dice != null
                && dice.Capabilities.IsPlayerPassThrough
                && !dice.IsSinkErasing;
        }

        public static bool AllowsDiceSwapThrough(DiceController dice) {
            return dice != null && dice.Capabilities.AllowsDiceSwapThrough;
        }

        public static bool IsGhostKind(DiceKind kind) {
            return kind == DiceKind.Ghost;
        }

        /// <summary>
        /// Player may walk this cell as floor: empty, or only pass-through dice occupy it.
        /// </summary>
        public static bool IsPlayerFloorPassable(DiceRegistry registry, Vector2Int cell) {
            if (registry == null) {
                return false;
            }

            if (registry.CanPlaceBottomDiceAt(cell)) {
                return true;
            }

            if (!registry.TryGetBottomIncludingPending(cell, out var bottom)
                || bottom == null
                || !IsPlayerPassThrough(bottom)) {
                return false;
            }

            if (registry.TryGetTopAt(cell, out var top)
                && top != null
                && !IsPlayerPassThrough(top)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Non-ghost moving onto a ghost bottom (same tier) → cell swap.
        /// Ghost-to-ghost cannot initiate: ghost is not player-movable.
        /// </summary>
        public static bool TryResolveCellSwap(
            DiceState moverFrom,
            DiceController ghost,
            out DiceState moverTo,
            out DiceState ghostFrom,
            out DiceState ghostTo) {
            moverTo = default;
            ghostFrom = default;
            ghostTo = default;

            if (!AllowsDiceSwapThrough(ghost) || IsGhostKind(moverFrom.Kind)) {
                return false;
            }

            var ghostState = ghost.CurrentState;
            if (ghostState.Tier != moverFrom.Tier) {
                return false;
            }

            if (ghost.IsBusy || ghost.IsErasing || ghost.IsVanishing || ghost.IsCarried) {
                return false;
            }

            moverTo = new DiceState(
                ghostState.GridPos,
                moverFrom.Orientation,
                moverFrom.Tier,
                moverFrom.Kind);
            ghostFrom = ghostState;
            ghostTo = new DiceState(
                moverFrom.GridPos,
                ghostState.Orientation,
                ghostState.Tier,
                ghostState.Kind);
            return true;
        }

        /// <summary>
        /// Non-ghost trying to stack on ghost bottom → same cell, mover Bottom, ghost Top.
        /// Ghost stacking on ghost is a normal top placement (no swap).
        /// </summary>
        public static bool TryResolveInCellPromote(
            DiceState moverFrom,
            DiceController ghostBottom,
            out DiceState moverTo,
            out DiceState ghostFrom,
            out DiceState ghostTo) {
            moverTo = default;
            ghostFrom = default;
            ghostTo = default;

            if (!AllowsDiceSwapThrough(ghostBottom) || IsGhostKind(moverFrom.Kind)) {
                return false;
            }

            var ghostState = ghostBottom.CurrentState;
            if (ghostState.Tier != DiceStackTier.Bottom) {
                return false;
            }

            if (ghostBottom.IsBusy || ghostBottom.IsErasing || ghostBottom.IsVanishing || ghostBottom.IsCarried) {
                return false;
            }

            moverTo = new DiceState(
                ghostState.GridPos,
                moverFrom.Orientation,
                DiceStackTier.Bottom,
                moverFrom.Kind);
            ghostFrom = ghostState;
            ghostTo = new DiceState(
                ghostState.GridPos,
                ghostState.Orientation,
                DiceStackTier.Top,
                ghostState.Kind);
            return true;
        }

        public static bool ShouldInCellPromoteOnTopPlacement(
            DiceController bottom,
            DiceKind incomingKind) {
            return AllowsDiceSwapThrough(bottom) && !IsGhostKind(incomingKind);
        }
    }
}
