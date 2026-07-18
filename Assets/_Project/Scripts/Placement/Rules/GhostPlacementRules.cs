using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class GhostPlacementRules
    {
        public static bool IsPlayerPassThrough(DiceController dice) {
            return dice != null && dice.EffectiveBehavior.IsPlayerPassThrough;
        }

        public static bool AllowsDiceSwapThrough(DiceController dice) {
            return dice != null && dice.Capabilities.AllowsDiceSwapThrough;
        }

        public static bool IsPassThroughKind(DiceKind kind) {
            return DiceBehaviorResolver.GetBehavior(kind).Capabilities.IsPlayerPassThrough;
        }

        /// <summary>
        /// True when the cell has a Top die that blocks player standing / Bottom occlusion.
        /// Pass-through (Ghost) tops do not count.
        /// </summary>
        public static bool HasSolidTopAt(DiceRegistry registry, Vector2Int cell) {
            return registry != null
                && registry.TryGetTopAt(cell, out var top)
                && top != null
                && !IsPlayerPassThrough(top);
        }

        public static bool HasSolidBottomAt(DiceRegistry registry, Vector2Int cell) {
            return registry != null
                && registry.TryGetBottomAt(cell, out var bottom)
                && bottom != null
                && !IsPlayerPassThrough(bottom);
        }

        /// <summary>
        /// Bottom slot is free for solid occupancy (ghosts do not occupy for collision).
        /// </summary>
        public static bool CanPlaceSolidBottomAt(DiceRegistry registry, Vector2Int cell) {
            if (registry == null || registry.Board == null
                || !registry.Board.IsInside(cell)
                || registry.Board.GetCell(cell) != CellType.Floor) {
                return false;
            }

            if (HasSolidBottomAt(registry, cell)) {
                return false;
            }

            if (registry.TryGetPendingBottomAt(cell, out var pending)
                && pending != null
                && !IsPlayerPassThrough(pending)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Top slot is free for solid occupancy: solid Bottom present, no solid Top (ghost Top ignored).
        /// </summary>
        public static bool CanPlaceSolidTopAt(DiceRegistry registry, Vector2Int cell) {
            return HasSolidBottomAt(registry, cell) && !HasSolidTopAt(registry, cell);
        }

        /// <summary>
        /// Player may walk this cell as floor when no solid dice occupy it (ghosts are not obstacles).
        /// </summary>
        public static bool IsPlayerFloorPassable(DiceRegistry registry, Vector2Int cell) {
            if (registry == null) {
                return false;
            }

            return !HasSolidTopAt(registry, cell) && !HasSolidBottomAt(registry, cell)
                && !(registry.TryGetPendingBottomAt(cell, out var pending)
                    && pending != null
                    && !IsPlayerPassThrough(pending));
        }

        /// <summary>
        /// Non-ghost moving onto a ghost bottom (same tier) → cell swap.
        /// Pass-through movers cannot initiate: they are not player-movable.
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

            if (!AllowsDiceSwapThrough(ghost) || IsPassThroughKind(moverFrom.Kind)) {
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
        /// Pass-through stacking on ghost is a normal top placement (no swap).
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

            if (!AllowsDiceSwapThrough(ghostBottom) || IsPassThroughKind(moverFrom.Kind)) {
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
            return AllowsDiceSwapThrough(bottom) && !IsPassThroughKind(incomingKind);
        }
    }
}
