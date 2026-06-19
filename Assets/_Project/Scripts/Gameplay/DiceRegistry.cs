using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DiceRegistry : MonoBehaviour
    {
        struct GridStack {
            public DiceController Bottom;
            public DiceController Top;
        }

        readonly Dictionary<Vector2Int, GridStack> byGrid = new();
        readonly List<DiceController> allDice = new();

        public IReadOnlyList<DiceController> AllDice => allDice;

        public void Register(DiceController dice) {
            if (dice == null || allDice.Contains(dice)) {
                return;
            }

            allDice.Add(dice);
            SetDiceAt(dice.CurrentState.GridPos, dice);
        }

        public void Unregister(DiceController dice) {
            if (dice == null) {
                return;
            }

            allDice.Remove(dice);
            ClearDiceAt(dice.CurrentState.GridPos, dice);
        }

        public void MoveDice(DiceController dice, Vector2Int from, Vector2Int to) {
            ClearDiceAt(from, dice);
            SetDiceAt(to, dice);
        }

        public bool TryGetBottomAt(Vector2Int gridPos, out DiceController dice) {
            dice = null;
            if (!byGrid.TryGetValue(gridPos, out var stack) || stack.Bottom == null) {
                return false;
            }

            dice = stack.Bottom;
            return true;
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

        public DiceController GetNeighbor(DiceController dice, Direction direction) {
            if (dice == null) {
                return null;
            }

            var neighborPos = dice.CurrentState.GridPos + direction.ToGridDelta();
            TryGetBottomAt(neighborPos, out var neighbor);
            return neighbor;
        }

        public DiceController GetFacingDiceAt(DiceController fromDice, Direction direction) {
            if (fromDice == null) {
                return null;
            }

            var neighborPos = fromDice.CurrentState.GridPos + direction.ToGridDelta();
            if (TryGetTopAt(neighborPos, out var topDice)) {
                return topDice;
            }

            if (TryGetBottomAt(neighborPos, out var bottomDice)) {
                return bottomDice;
            }

            return null;
        }

        public DiceController GetTransferTargetAt(
            DiceController fromDice,
            Direction direction,
            DiceStackTier standingTier) {
            if (fromDice == null) {
                return null;
            }

            var neighborPos = fromDice.CurrentState.GridPos + direction.ToGridDelta();
            if (standingTier == DiceStackTier.Bottom) {
                if (TryGetBottomAt(neighborPos, out var bottomDice)) {
                    return bottomDice;
                }

                if (TryGetTopAt(neighborPos, out var topDice)) {
                    return topDice;
                }

                return null;
            }

            if (TryGetTopAt(neighborPos, out var top)) {
                return top;
            }

            if (TryGetBottomAt(neighborPos, out var bottom)) {
                return bottom;
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

        void SetDiceAt(Vector2Int gridPos, DiceController dice) {
            if (!byGrid.TryGetValue(gridPos, out var stack)) {
                stack = default;
            }

            if (dice.CurrentState.Tier == DiceStackTier.Top) {
                stack.Top = dice;
            } else {
                stack.Bottom = dice;
            }

            byGrid[gridPos] = stack;
        }

        void ClearDiceAt(Vector2Int gridPos, DiceController dice) {
            if (!byGrid.TryGetValue(gridPos, out var stack)) {
                return;
            }

            if (dice.CurrentState.Tier == DiceStackTier.Top) {
                stack.Top = null;
            } else {
                stack.Bottom = null;
            }

            if (stack.Bottom == null && stack.Top == null) {
                byGrid.Remove(gridPos);
            } else {
                byGrid[gridPos] = stack;
            }
        }
    }
}
