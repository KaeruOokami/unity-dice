using System.Collections.Generic;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DiceRegistry : MonoBehaviour
    {
        readonly Dictionary<Vector2Int, DiceController> byGrid = new();

        public void Register(DiceController dice) {
            if (dice == null) {
                return;
            }

            byGrid[dice.CurrentState.GridPos] = dice;
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
    }
}
