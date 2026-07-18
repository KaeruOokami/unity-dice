using System;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public class DiceRegistry : MonoBehaviour, IDicePlacement
    {
        struct GridStack {
            public DiceController Bottom;
            public DiceController Top;
        }

        readonly Dictionary<Vector2Int, GridStack> byGrid = new();
        readonly Dictionary<Vector2Int, GridStack> pendingSpawns = new();
        readonly List<DiceController> allDice = new();
        readonly List<Vector2Int> footprintCells = new(JumboFootprint.CellCount);
        readonly List<DiceController> footprintClearBuffer = new();

        Board board;

        public IReadOnlyList<DiceController> AllDice => allDice;

        public Board Board => board;

        public void Configure(Board targetBoard) {
            board = targetBoard;
        }

        public bool CanPlaceBottomDiceAt(Vector2Int gridPos) {
            if (board == null || !board.IsInside(gridPos) || board.GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            return !HasBottomAt(gridPos) && !HasPendingBottomAt(gridPos);
        }

        public bool CanPlaceTopDiceAt(Vector2Int gridPos) {
            if (board == null || !board.IsInside(gridPos) || board.GetCell(gridPos) != CellType.Floor) {
                return false;
            }

            return HasBottomAt(gridPos) && !HasTopAt(gridPos) && !HasPendingTopAt(gridPos);
        }

        public bool CanAcceptTopDiceAt(Vector2Int gridPos) {
            if (CanPlaceTopDiceAt(gridPos)) {
                return true;
            }

            return TryGetTopAt(gridPos, out var top)
                && top != null
                && top.IsRadianceErasing;
        }

        public bool HasErasingDiceAt(Vector2Int gridPos) {
            foreach (var dice in allDice) {
                if (dice != null && dice.IsErasing && dice.CurrentState.GridPos == gridPos) {
                    return true;
                }
            }

            return false;
        }

        public void RemoveFromGrid(DiceController dice) {
            if (dice == null) {
                return;
            }

            Remove(dice, dice.CurrentState.GridPos, dice.CurrentState.Tier);
        }

        public void RestoreToGrid(DiceController dice) {
            if (dice == null || dice.IsErasureGhost) {
                return;
            }

            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            SetDiceAt(dice.CurrentState.GridPos, dice, dice.CurrentState.Tier);
        }

        public void EvictErasingDiceAt(Vector2Int gridPos) {
            DiceController topErasing = null;
            DiceController bottomGhost = null;
            DiceController topRadiance = null;
            DiceController bottomRadiance = null;

            foreach (var dice in allDice) {
                if (dice == null || !dice.IsErasing || dice.CurrentState.GridPos != gridPos) {
                    continue;
                }

                if (dice.IsErasureGhost) {
                    if (dice.CurrentState.Tier == DiceStackTier.Top) {
                        topErasing = dice;
                    } else {
                        bottomGhost = dice;
                    }

                    continue;
                }

                if (dice.IsRadianceErasing) {
                    if (dice.CurrentState.Tier == DiceStackTier.Top) {
                        topRadiance = dice;
                    } else {
                        bottomRadiance = dice;
                    }
                }
            }

            topErasing?.CompleteErasureFromOverride();
            bottomGhost?.CompleteErasureFromOverride();
            topRadiance?.CompleteErasureFromOverride();
            bottomRadiance?.CompleteErasureFromOverride();
        }

        public void RegisterPendingSpawn(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            ClearPendingSpawn(dice);

            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            if (dice.Capabilities.HasExpandedFootprint) {
                footprintCells.Clear();
                JumboFootprint.AppendCells(gridPos, footprintCells);
                for (var i = 0; i < footprintCells.Count; i++) {
                    RegisterPendingSpawnAtCell(dice, footprintCells[i], DiceStackTier.Bottom);
                    RegisterPendingSpawnAtCell(dice, footprintCells[i], DiceStackTier.Top);
                }

                return;
            }

            RegisterPendingSpawnAtCell(dice, gridPos, tier);
        }

        void RegisterPendingSpawnAtCell(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (!pendingSpawns.TryGetValue(gridPos, out var stack)) {
                stack = default;
            }

            if (tier == DiceStackTier.Top) {
                stack.Top = dice;
            } else {
                stack.Bottom = dice;
            }

            pendingSpawns[gridPos] = stack;
        }

        public void CommitPendingSpawn(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            ClearPendingSpawn(dice);

            if (dice.Capabilities.HasExpandedFootprint) {
                PlaceJumbo(dice, gridPos);
                return;
            }

            var resolvedTier = ResolveSpawnCommitTier(gridPos, tier);
            if (resolvedTier != tier) {
                dice.ApplyExternalState(
                    new DiceState(
                        gridPos,
                        dice.CurrentState.Orientation,
                        resolvedTier,
                        dice.Kind),
                    snapVisual: true);
            }

            Place(dice, gridPos, resolvedTier);
        }

        /// <summary>
        /// If Bottom was taken while a Bottom spawn was still pending, commit as Top when possible.
        /// </summary>
        DiceStackTier ResolveSpawnCommitTier(Vector2Int gridPos, DiceStackTier requestedTier) {
            if (requestedTier == DiceStackTier.Bottom) {
                if (!HasBottomAt(gridPos)) {
                    return DiceStackTier.Bottom;
                }

                if (!HasTopAt(gridPos) && !HasPendingTopAt(gridPos)) {
                    return DiceStackTier.Top;
                }

                Debug.LogError(
                    $"DiceRegistry: spawn commit cannot place at ({gridPos.x},{gridPos.y}); " +
                    "Bottom occupied and Top unavailable.");
                return DiceStackTier.Top;
            }

            if (!HasBottomAt(gridPos) || HasTopAt(gridPos) || HasPendingTopAt(gridPos)) {
                Debug.LogError(
                    $"DiceRegistry: spawn commit Top unavailable at ({gridPos.x},{gridPos.y}).");
            }

            return DiceStackTier.Top;
        }

        public void Place(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            if (dice.Capabilities.HasExpandedFootprint) {
                PlaceJumbo(dice, gridPos);
                return;
            }

            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            EvictErasingDiceAt(gridPos);

            if (tier == DiceStackTier.Top
                && TryGetBottomAt(gridPos, out var ghostBottom)
                && !HasTopAt(gridPos)
                && GhostPlacementRules.ShouldInCellPromoteOnTopPlacement(ghostBottom, dice.Kind)) {
                ClearDiceAt(gridPos, ghostBottom, DiceStackTier.Bottom, notifySupportLoss: false);
                SetDiceAt(gridPos, dice, DiceStackTier.Bottom);
                SetDiceAt(gridPos, ghostBottom, DiceStackTier.Top);
                dice.ApplyExternalState(
                    new DiceState(
                        gridPos,
                        dice.CurrentState.Orientation,
                        DiceStackTier.Bottom,
                        dice.Kind),
                    snapVisual: true);
                ghostBottom.ApplyExternalState(
                    new DiceState(
                        gridPos,
                        ghostBottom.CurrentState.Orientation,
                        DiceStackTier.Top,
                        ghostBottom.Kind),
                    snapVisual: true);
                return;
            }

            SetDiceAt(gridPos, dice, tier);
        }

        void PlaceJumbo(DiceController dice, Vector2Int anchor) {
            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            ClearOccupantsForJumboLanding(anchor, excluding: dice);
            footprintCells.Clear();
            JumboFootprint.AppendCells(anchor, footprintCells);
            for (var i = 0; i < footprintCells.Count; i++) {
                var cell = footprintCells[i];
                SetDiceAt(cell, dice, DiceStackTier.Bottom);
                SetDiceAt(cell, dice, DiceStackTier.Top);
            }

            if (dice.CurrentState.GridPos != anchor
                || dice.CurrentState.Tier != DiceStackTier.Bottom) {
                dice.ApplyExternalState(
                    new DiceState(
                        anchor,
                        dice.CurrentState.Orientation,
                        DiceStackTier.Bottom,
                        dice.Kind),
                    snapVisual: true);
            }
        }

        void ClearOccupantsForJumboLanding(Vector2Int anchor, DiceController excluding) {
            footprintClearBuffer.Clear();
            footprintCells.Clear();
            JumboFootprint.AppendCells(anchor, footprintCells);

            for (var i = 0; i < footprintCells.Count; i++) {
                var cell = footprintCells[i];
                if (TryGetBottomAt(cell, out var bottom)
                    && bottom != null
                    && bottom != excluding
                    && !footprintClearBuffer.Contains(bottom)) {
                    footprintClearBuffer.Add(bottom);
                }

                if (TryGetTopAt(cell, out var top)
                    && top != null
                    && top != excluding
                    && !footprintClearBuffer.Contains(top)) {
                    footprintClearBuffer.Add(top);
                }
            }

            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                if (dice == null || dice == excluding || footprintClearBuffer.Contains(dice)) {
                    continue;
                }

                if (OccupiesAnyCellInAnchorFootprint(dice, anchor)) {
                    footprintClearBuffer.Add(dice);
                }
            }

            for (var i = 0; i < footprintClearBuffer.Count; i++) {
                footprintClearBuffer[i].ForceDestroyForOverride();
            }

            footprintClearBuffer.Clear();
        }

        static bool OccupiesAnyCellInAnchorFootprint(DiceController dice, Vector2Int anchor) {
            if (dice == null) {
                return false;
            }

            if (!dice.Capabilities.HasExpandedFootprint) {
                return JumboFootprint.Contains(anchor, dice.CurrentState.GridPos);
            }

            var otherAnchor = dice.CurrentState.GridPos;
            for (var dx = 0; dx < JumboFootprint.Size; dx++) {
                for (var dy = 0; dy < JumboFootprint.Size; dy++) {
                    var cell = new Vector2Int(otherAnchor.x + dx, otherAnchor.y + dy);
                    if (JumboFootprint.Contains(anchor, cell)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryApplyGhostLanding(
            DiceController mover,
            DiceState moverFrom,
            DiceState moverTo,
            GhostLandingMode mode,
            DiceState ghostFrom,
            DiceState ghostTo) {
            if (mover == null || mode == GhostLandingMode.None) {
                return false;
            }

            if (!TryDeferGhostOccupant(ghostFrom, out var ghost)) {
                return false;
            }

            ClearDiceAt(moverFrom.GridPos, mover, moverFrom.Tier, notifySupportLoss: false);
            SetDiceAt(moverTo.GridPos, mover, moverTo.Tier);
            if (!TryCompleteDeferredGhostLanding(ghost, ghostFrom, ghostTo)) {
                return false;
            }

            TryResolveUnsupportedTopAt(moverFrom.GridPos, mover);
            TryResolveUnsupportedTopAt(ghostFrom.GridPos, ghost);
            return true;
        }

        /// <summary>
        /// Remove ghost from the grid slot but leave its <see cref="DiceController.CurrentState"/> unchanged
        /// so displace can run after the mover animation completes.
        /// </summary>
        public bool TryDeferGhostOccupant(DiceState ghostFrom, out DiceController ghost) {
            ghost = null;
            if (!TryGetDiceAt(ghostFrom.GridPos, ghostFrom.Tier, out ghost) || ghost == null) {
                Debug.LogError(
                    $"DiceRegistry: deferred ghost missing at ({ghostFrom.GridPos.x},{ghostFrom.GridPos.y}) tier={ghostFrom.Tier}");
                return false;
            }

            if (!GhostPlacementRules.AllowsDiceSwapThrough(ghost)) {
                Debug.LogError($"DiceRegistry: deferred ghost is not swap-through kind={ghost.Kind}");
                ghost = null;
                return false;
            }

            ClearDiceAt(ghostFrom.GridPos, ghost, ghostFrom.Tier, notifySupportLoss: false);
            return true;
        }

        /// <summary>
        /// Place a previously deferred ghost at its destination and play displace visual.
        /// </summary>
        public bool TryCompleteDeferredGhostLanding(
            DiceController ghost,
            DiceState ghostFrom,
            DiceState ghostTo,
            Action<DiceController> onComplete = null) {
            if (ghost == null) {
                onComplete?.Invoke(null);
                return false;
            }

            SetDiceAt(ghostTo.GridPos, ghost, ghostTo.Tier);
            ghost.ApplyExternalState(ghostTo, snapVisual: false);
            ghost.PlayGhostDisplaceVisual(ghostFrom, ghostTo, () => onComplete?.Invoke(ghost));
            return true;
        }

        public bool TryCompleteDeferredGhostLanding(
            DiceState ghostFrom,
            DiceState ghostTo,
            Action<DiceController> onComplete = null) {
            // Ghost was deferred: still holds ghostFrom in CurrentState, not in grid.
            DiceController ghost = null;
            foreach (var dice in allDice) {
                if (dice == null || !GhostPlacementRules.AllowsDiceSwapThrough(dice)) {
                    continue;
                }

                var state = dice.CurrentState;
                if (state.GridPos == ghostFrom.GridPos && state.Tier == ghostFrom.Tier) {
                    ghost = dice;
                    break;
                }
            }

            if (ghost == null) {
                Debug.LogError(
                    $"DiceRegistry: deferred ghost not found for complete at ({ghostFrom.GridPos.x},{ghostFrom.GridPos.y}) tier={ghostFrom.Tier}");
                onComplete?.Invoke(null);
                return false;
            }

            return TryCompleteDeferredGhostLanding(ghost, ghostFrom, ghostTo, onComplete);
        }

        public void Remove(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            if (dice.Capabilities.HasExpandedFootprint) {
                footprintCells.Clear();
                JumboFootprint.AppendCells(dice.CurrentState.GridPos, footprintCells);
                for (var i = 0; i < footprintCells.Count; i++) {
                    ClearDiceAt(footprintCells[i], dice, DiceStackTier.Bottom, notifySupportLoss: false);
                    ClearDiceAt(footprintCells[i], dice, DiceStackTier.Top, notifySupportLoss: false);
                }

                return;
            }

            ClearDiceAt(gridPos, dice, tier);
        }

            public void Unregister(DiceController dice) {
            if (dice == null) {
                return;
            }

            ClearPendingSpawn(dice);
            Remove(dice, dice.CurrentState.GridPos, dice.CurrentState.Tier);
            allDice.Remove(dice);
        }

        public void MoveDice(
            DiceController dice,
            Vector2Int from,
            Vector2Int to,
            DiceStackTier fromTier,
            DiceStackTier toTier) {
            EvictErasingDiceAt(to);
            ClearDiceAt(from, dice, fromTier, notifySupportLoss: false);
            SetDiceAt(to, dice, toTier);
            TryResolveUnsupportedTopAt(from, dice);
        }

        public bool TryGetDiceAt(Vector2Int gridPos, DiceStackTier tier, out DiceController dice) {
            return tier == DiceStackTier.Top
                ? TryGetTopAt(gridPos, out dice)
                : TryGetBottomAt(gridPos, out dice);
        }

        public bool TryGetBottomAt(Vector2Int gridPos, out DiceController dice) {
            dice = null;
            if (!byGrid.TryGetValue(gridPos, out var stack) || stack.Bottom == null) {
                return false;
            }

            dice = stack.Bottom;
            return true;
        }

        public bool TryGetPendingBottomAt(Vector2Int gridPos, out DiceController dice) {
            dice = null;
            if (!pendingSpawns.TryGetValue(gridPos, out var stack) || stack.Bottom == null) {
                return false;
            }

            dice = stack.Bottom;
            return true;
        }

        public bool TryGetBottomIncludingPending(Vector2Int gridPos, out DiceController dice) {
            if (TryGetBottomAt(gridPos, out dice)) {
                return true;
            }

            return TryGetPendingBottomAt(gridPos, out dice);
        }

        public void SyncStackedTopAt(Vector2Int gridPos, Board board) {
            if (board == null || !TryGetTopAt(gridPos, out var top) || top == null) {
                return;
            }

            top.View?.SyncStackedSurface(top.CurrentState, board, this);
        }

        public bool TryGetTopAt(Vector2Int gridPos, out DiceController dice) {
            dice = null;
            if (!byGrid.TryGetValue(gridPos, out var stack) || stack.Top == null) {
                return false;
            }

            dice = stack.Top;
            return true;
        }

        public bool TryGetAt(Vector2Int gridPos, out DiceController dice) {
            if (TryGetTopAt(gridPos, out dice)) {
                return true;
            }

            return TryGetBottomAt(gridPos, out dice);
        }

        public bool HasTopAt(Vector2Int gridPos) {
            return byGrid.TryGetValue(gridPos, out var stack) && stack.Top != null;
        }

        public bool HasBottomAt(Vector2Int gridPos) {
            return byGrid.TryGetValue(gridPos, out var stack) && stack.Bottom != null;
        }

        public bool HasPendingBottomAt(Vector2Int gridPos) {
            return pendingSpawns.TryGetValue(gridPos, out var stack) && stack.Bottom != null;
        }

        public bool HasPendingTopAt(Vector2Int gridPos) {
            return pendingSpawns.TryGetValue(gridPos, out var stack) && stack.Top != null;
        }

        public bool TryGetPendingTopAt(Vector2Int gridPos, out DiceController dice) {
            dice = null;
            if (!pendingSpawns.TryGetValue(gridPos, out var stack) || stack.Top == null) {
                return false;
            }

            dice = stack.Top;
            return true;
        }

        void ClearPendingSpawn(DiceController dice) {
            if (dice == null || pendingSpawns.Count == 0) {
                return;
            }

            var emptyCells = new List<Vector2Int>();
            foreach (var pair in pendingSpawns) {
                var stack = pair.Value;
                var changed = false;

                if (stack.Bottom == dice) {
                    stack.Bottom = null;
                    changed = true;
                }

                if (stack.Top == dice) {
                    stack.Top = null;
                    changed = true;
                }

                if (!changed) {
                    continue;
                }

                if (stack.Bottom == null && stack.Top == null) {
                    emptyCells.Add(pair.Key);
                } else {
                    pendingSpawns[pair.Key] = stack;
                }
            }

            for (var i = 0; i < emptyCells.Count; i++) {
                pendingSpawns.Remove(emptyCells[i]);
            }
        }

        public bool BlocksTraversalBetween(Vector2Int fromCell, Vector2Int toCell) {
            return board != null && board.BlocksMovement(fromCell, toCell, null);
        }

        public DiceController GetNeighbor(DiceController dice, Direction direction) {
            if (dice == null) {
                return null;
            }

            var neighborPos = dice.CurrentState.GridPos + direction.ToGridDelta();
            if (dice.CurrentState.Tier == DiceStackTier.Top) {
                TryGetTopAt(neighborPos, out var topNeighbor);
                return topNeighbor;
            }

            TryGetBottomAt(neighborPos, out var bottomNeighbor);
            return bottomNeighbor;
        }

        public DiceController GetFacingDiceAt(DiceController fromDice, Direction direction) {
            if (fromDice == null) {
                return null;
            }

            var neighborPos = fromDice.CurrentState.GridPos + direction.ToGridDelta();
            if (fromDice.CurrentState.Tier == DiceStackTier.Top) {
                TryGetTopAt(neighborPos, out var topDice);
                return topDice;
            }

            TryGetBottomAt(neighborPos, out var bottomDice);
            return bottomDice;
        }

        public DiceController GetTransferTargetAt(
            DiceController fromDice,
            Direction direction,
            int fromLevel) {
            if (fromDice == null) {
                return null;
            }

            var neighborPos = fromDice.CurrentState.GridPos + direction.ToGridDelta();
            if (SurfaceHeightLevel.IsAtOrAboveTop(fromLevel)) {
                if (TryGetTopAt(neighborPos, out var top)
                    && top != null
                    && !GhostPlacementRules.IsPlayerPassThrough(top)) {
                    return top;
                }

                return null;
            }

            TryGetBottomAt(neighborPos, out var bottom);
            if (bottom != null && !GhostPlacementRules.IsPlayerPassThrough(bottom)) {
                return bottom;
            }

            if (TryGetPendingBottomAt(neighborPos, out var pendingBottom)
                && pendingBottom != null
                && pendingBottom.AllowsUnconditionalMount
                && !GhostPlacementRules.IsPlayerPassThrough(pendingBottom)) {
                return pendingBottom;
            }

            return null;
        }

        public DiceController ResolveSupportBottom(DiceController dice) {
            if (dice == null) {
                return null;
            }

            if (dice.CurrentState.Tier == DiceStackTier.Bottom) {
                return dice;
            }

            TryGetBottomAt(dice.CurrentState.GridPos, out var bottom);
            return bottom;
        }

        public bool AnyRolling() {
            foreach (var dice in allDice) {
                if (dice != null && dice.IsRolling) {
                    return true;
                }
            }

            return false;
        }

        public bool AnyCarried() {
            foreach (var dice in allDice) {
                if (dice != null && dice.IsCarried) {
                    return true;
                }
            }

            return false;
        }

        void SetDiceAt(Vector2Int gridPos, DiceController dice, DiceStackTier tier) {
            if (!byGrid.TryGetValue(gridPos, out var stack)) {
                stack = default;
            }

            if (tier == DiceStackTier.Bottom
                && stack.Bottom != null
                && stack.Bottom != dice
                && !stack.Bottom.IsErasureGhost) {
                Debug.LogError(
                    $"DiceRegistry: overwriting bottom at ({gridPos.x},{gridPos.y}) " +
                    $"existing={stack.Bottom.name} incoming={dice?.name}");
            }

            if (tier == DiceStackTier.Top) {
                stack.Top = dice;
            } else {
                stack.Bottom = dice;
            }

            byGrid[gridPos] = stack;

            if (tier == DiceStackTier.Bottom) {
                TryRetargetPendingBottomSpawnAt(gridPos, excluding: dice);
            }
        }

        /// <summary>
        /// When a committed Bottom claims a cell that still has a pending Bottom spawn,
        /// retarget that spawn to Top immediately (reservation + spawning die state/visual).
        /// Pending Top is never interrupted this way — coupled entry is blocked while Top is reserved.
        /// </summary>
        void TryRetargetPendingBottomSpawnAt(Vector2Int gridPos, DiceController excluding) {
            if (!TryGetPendingBottomAt(gridPos, out var pending) || pending == null || pending == excluding) {
                return;
            }

            if (HasTopAt(gridPos) || HasPendingTopAt(gridPos)) {
                Debug.LogError(
                    $"DiceRegistry: cannot retarget pending Bottom spawn at ({gridPos.x},{gridPos.y}); " +
                    "Top already occupied.");
                return;
            }

            if (!pendingSpawns.TryGetValue(gridPos, out var pendingStack)) {
                return;
            }

            pendingStack.Bottom = null;
            pendingStack.Top = pending;
            pendingSpawns[gridPos] = pendingStack;
            pending.NotifyPendingSpawnRetargetedToTop();
        }

        void ClearDiceAt(
            Vector2Int gridPos,
            DiceController dice,
            DiceStackTier tier,
            bool notifySupportLoss = true) {
            if (!byGrid.TryGetValue(gridPos, out var stack)) {
                return;
            }

            DiceController removedBottom = null;
            DiceController topAfterBottomRemoved = null;

            if (tier == DiceStackTier.Top) {
                if (stack.Top == dice) {
                    stack.Top = null;
                }
            } else if (stack.Bottom == dice) {
                stack.Bottom = null;
                removedBottom = dice;
                topAfterBottomRemoved = stack.Top;
            }

            if (stack.Bottom == null && stack.Top == null) {
                byGrid.Remove(gridPos);
            } else {
                byGrid[gridPos] = stack;
            }

            if (notifySupportLoss && removedBottom != null) {
                NotifyTopAfterBottomRemoved(gridPos, removedBottom, topAfterBottomRemoved);
            }
        }

        /// <summary>
        /// After a batched clear/place, demote Top only if Bottom is still without solid support.
        /// Ghost Bottom does not count as support (same as empty for collision).
        /// </summary>
        public void ResolveUnsupportedTopAt(Vector2Int gridPos) {
            TryResolveUnsupportedTopAt(gridPos, removedBottom: null);
        }

        void TryResolveUnsupportedTopAt(Vector2Int gridPos, DiceController removedBottom) {
            if (!TryGetTopAt(gridPos, out var top) || top == null) {
                return;
            }

            if (GhostPlacementRules.HasSolidBottomAt(this, gridPos)) {
                return;
            }

            NotifyTopAfterBottomRemoved(gridPos, removedBottom, top);
        }

        static void NotifyTopAfterBottomRemoved(
            Vector2Int gridPos,
            DiceController removedBottom,
            DiceController topDice) {
            if (topDice == null || topDice == removedBottom) {
                return;
            }

            if (topDice.CurrentState.GridPos != gridPos
                || topDice.CurrentState.Tier != DiceStackTier.Top) {
                return;
            }

            topDice.OnBottomSupportLost(removedBottom);
        }
    }
}
