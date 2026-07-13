using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct AiNavigationState
    {
        public Vector2Int Cell { get; }
        public int Level { get; }
        public DiceController StandingDice { get; }

        public AiNavigationState(Vector2Int cell, int level, DiceController standingDice) {
            Cell = cell;
            Level = level;
            StandingDice = standingDice;
        }

        public bool Equals(AiNavigationState other) {
            return Cell == other.Cell
                && Level == other.Level
                && StandingDice == other.StandingDice;
        }

        public override bool Equals(object obj) {
            return obj is AiNavigationState other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                var hash = Cell.GetHashCode();
                hash = (hash * 397) ^ Level;
                hash = (hash * 397) ^ (StandingDice != null ? StandingDice.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
