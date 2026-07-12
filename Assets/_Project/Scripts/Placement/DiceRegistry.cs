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

        Board board;

        public IReadOnlyList<DiceController> AllDice => allDice;

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
            Place(dice, gridPos, tier);
        }

        public void Place(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
                return;
            }

            if (!allDice.Contains(dice)) {
                allDice.Add(dice);
            }

            EvictErasingDiceAt(gridPos);
            SetDiceAt(gridPos, dice, tier);
        }

        public void Remove(DiceController dice, Vector2Int gridPos, DiceStackTier tier) {
            if (dice == null) {
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
            ClearDiceAt(from, dice, fromTier);
            SetDiceAt(to, dice, toTier);
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

        bool HasPendingBottomAt(Vector2Int gridPos) {
            return pendingSpawns.TryGetValue(gridPos, out var stack) && stack.Bottom != null;
        }

        bool HasPendingTopAt(Vector2Int gridPos) {
            return pendingSpawns.TryGetValue(gridPos, out var stack) && stack.Top != null;
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
                TryGetTopAt(neighborPos, out var top);
                return top;
            }

            TryGetBottomAt(neighborPos, out var bottom);
            return bottom;
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
        }

        void ClearDiceAt(Vector2Int gridPos, DiceController dice, DiceStackTier tier) {
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

            if (removedBottom != null) {
                NotifyTopAfterBottomRemoved(gridPos, removedBottom, topAfterBottomRemoved);
            }
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
