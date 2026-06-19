using System.Collections.Generic;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DiceRegistry : MonoBehaviour
    {
        readonly Dictionary<Vector2Int, DiceController> byGrid = new();
        readonly List<DiceController> allDice = new();

        public IReadOnlyList<DiceController> AllDice => allDice;

        public void Register(DiceController dice) {
            if (dice == null || allDice.Contains(dice)) {
                return;
            }

            allDice.Add(dice);
            byGrid[dice.CurrentState.GridPos] = dice;
        }

        public void Unregister(DiceController dice) {
            if (dice == null) {
                return;
            }

            allDice.Remove(dice);
            byGrid.Remove(dice.CurrentState.GridPos);
        }

        public void MoveDice(DiceController dice, Vector2Int from, Vector2Int to) {
            byGrid.Remove(from);
            byGrid[to] = dice;
        }

        public bool TryGetAt(Vector2Int gridPos, out DiceController dice) {
            return byGrid.TryGetValue(gridPos, out dice);
        }

        public DiceController GetNeighbor(DiceController dice, Direction direction) {
            if (dice == null) {
                return null;
            }

            var neighborPos = dice.CurrentState.GridPos + direction.ToGridDelta();
            TryGetAt(neighborPos, out var neighbor);
            return neighbor;
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
    }
}
