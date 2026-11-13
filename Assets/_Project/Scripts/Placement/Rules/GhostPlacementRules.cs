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

        /// <summary>
        /// Sink erasure ghost stays player-solid but may be overwritten by dice Place/Move
        /// (<see cref="DiceRegistry.EvictErasingDiceAt"/>).
        /// Emerging bottoms use the same crush rule while still underground
        /// (progress ≥ ghost threshold — reverse of sink).
        /// </summary>
        public static bool IsCrushableByDicePlacement(DiceController dice) {
            return dice != null && (dice.IsErasureGhost || dice.IsEmergingGhost);
        }

        /// <summary>
        /// Occupant is a player-solid Top surface (standing / coupled interrupt).
        /// Pass-through Kind Ghost still occupies the slot
        /// (<see cref="DiceRegistry.HasTopSlotClaimAt"/>); it only fails this interaction test.
        /// </summary>
        public static bool CountsAsSolidTopClaim(DiceController dice) {
            return dice != null && !IsPlayerPassThrough(dice);
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
                && CountsAsSolidTopClaim(top);
        }

        public static bool HasSolidBottomAt(DiceRegistry registry, Vector2Int cell) {
            return registry != null
                && registry.TryGetBottomAt(cell, out var bottom)
                && bottom != null
                && !IsPlayerPassThrough(bottom);
        }

        /// <summary>
        /// Bottom slot is free for solid dice landing (empty or crushable overwrite).
        /// Kind Ghost occupies the slot — swap via <see cref="GhostLandingResolver"/>, not "empty".
        /// Occupied solid Bottom is not placeable here — ascent uses <see cref="CanPlaceSolidTopAt"/>.
        /// Interrupt promote is only at write time, not in this empty-slot check.
        /// </summary>
        public static bool CanPlaceSolidBottomAt(DiceRegistry registry, Vector2Int cell) {
            if (registry == null || registry.Board == null
                || !registry.Board.IsInside(cell)
                || registry.Board.GetCell(cell) != CellType.Floor) {
                return false;
            }

            if (registry.TryGetBottomAt(cell, out var bottom)
                && bottom != null
                && !IsCrushableByDicePlacement(bottom)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Top slot is free for solid dice landing: solid Bottom present, Top slot unclaimed.
        /// Kind Ghost Top occupies the slot — ascent swap via <see cref="GhostLandingResolver"/>.
        /// Jumbo only accepts Top dice in Bottom-only sink stage (≤ half height).
        /// Sink erasure ghost Bottom is crushable Bottom-land only — not a stack base.
        /// </summary>
        public static bool CanPlaceSolidTopAt(DiceRegistry registry, Vector2Int cell) {
            if (registry == null
                || !HasSolidBottomAt(registry, cell)
                || registry.HasTopSlotClaimAt(cell)) {
                return false;
            }

            if (registry.TryGetBottomAt(cell, out var bottom)
                && bottom != null) {
                if (IsCrushableByDicePlacement(bottom)) {
                    return false;
                }

                if (bottom.Capabilities.HasExpandedFootprint
                    && bottom.WantsJumboTopOccupancy) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Coupled stack entry onto Top is blocked when a player-solid Top surface is claimed.
        /// Pass-through Top still occupies the slot but is not a standing surface — grid swap
        /// remains available through <see cref="GhostLandingResolver"/>.
        /// </summary>
        public static bool BlocksDiceCoupledStackEntry(DiceRegistry registry, Vector2Int cell) {
            return registry != null
                && registry.HasBottomAt(cell)
                && registry.HasSolidTopClaimAt(cell);
        }

        /// <summary>
        /// Player on floor may walk this cell when Bottom is clear.
        /// Top-only occupancy (e.g. lift-mounted radiance) does not block floor entry.
        /// </summary>
        public static bool IsPlayerFloorPassable(DiceRegistry registry, Vector2Int cell) {
            return HorizontalEntryPolicy.IsBottomSlotClearForFloorEntry(registry, cell);
        }

        /// <summary>
        /// Same-tier ghost overlap → cell swap to previous cell (same tier).
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
        /// Ascent (Bottom → Top ghost): diagonal swap with no intermediate.
        /// Mover takes Top; ghost goes to the mover's previous cell as Bottom.
        /// </summary>
        public static bool TryResolveAscentGhostSwap(
            DiceState moverFromBottom,
            DiceController topGhost,
            out DiceState moverTo,
            out DiceState ghostFrom,
            out DiceState ghostTo) {
            moverTo = default;
            ghostFrom = default;
            ghostTo = default;

            if (moverFromBottom.Tier != DiceStackTier.Bottom) {
                return false;
            }

            if (!AllowsDiceSwapThrough(topGhost) || IsPassThroughKind(moverFromBottom.Kind)) {
                return false;
            }

            var ghostState = topGhost.CurrentState;
            if (ghostState.Tier != DiceStackTier.Top) {
                return false;
            }

            if (topGhost.IsBusy || topGhost.IsErasing || topGhost.IsVanishing || topGhost.IsCarried) {
                return false;
            }

            moverTo = new DiceState(
                ghostState.GridPos,
                moverFromBottom.Orientation,
                DiceStackTier.Top,
                moverFromBottom.Kind);
            ghostFrom = ghostState;
            ghostTo = new DiceState(
                moverFromBottom.GridPos,
                ghostState.Orientation,
                DiceStackTier.Bottom,
                ghostState.Kind);
            return true;
        }

        /// <summary>
        /// Top entry into a cell with swap-through Bottom and Top ghosts.
        /// Final occupancy: mover Bottom at target, bottom ghost → Top, top ghost → mover previous cell.
        /// </summary>
        public static bool TryResolveGhostStackTopEntry(
            DiceState moverFromTop,
            DiceController topGhost,
            DiceController bottomGhost,
            out DiceState moverTo,
            out DiceState exitingTopGhostFrom,
            out DiceState exitingTopGhostTo) {
            moverTo = default;
            exitingTopGhostFrom = default;
            exitingTopGhostTo = default;

            if (moverFromTop.Tier != DiceStackTier.Top
                || !AllowsDiceSwapThrough(topGhost)
                || !AllowsDiceSwapThrough(bottomGhost)
                || IsPassThroughKind(moverFromTop.Kind)) {
                return false;
            }

            var topState = topGhost.CurrentState;
            var bottomState = bottomGhost.CurrentState;
            if (topState.Tier != DiceStackTier.Top
                || bottomState.Tier != DiceStackTier.Bottom
                || topState.GridPos != bottomState.GridPos) {
                return false;
            }

            if (topGhost.IsBusy || topGhost.IsErasing || topGhost.IsVanishing || topGhost.IsCarried
                || bottomGhost.IsBusy || bottomGhost.IsErasing || bottomGhost.IsVanishing
                || bottomGhost.IsCarried) {
                return false;
            }

            moverTo = new DiceState(
                topState.GridPos,
                moverFromTop.Orientation,
                DiceStackTier.Bottom,
                moverFromTop.Kind);
            exitingTopGhostFrom = topState;
            exitingTopGhostTo = new DiceState(
                moverFromTop.GridPos,
                topState.Orientation,
                DiceStackTier.Top,
                topState.Kind);
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
