using System.Collections.Generic;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct AiNavigationConstraints
    {
        public HashSet<Vector2Int> ForbiddenCells { get; }

        public AiNavigationConstraints(HashSet<Vector2Int> forbiddenCells) {
            ForbiddenCells = forbiddenCells;
        }

        public static AiNavigationConstraints None => new AiNavigationConstraints(null);

        public bool IsCellAllowed(Vector2Int cell, Vector2Int goalCell) {
            if (cell == goalCell) {
                return true;
            }

            return ForbiddenCells == null || !ForbiddenCells.Contains(cell);
        }
    }
}
