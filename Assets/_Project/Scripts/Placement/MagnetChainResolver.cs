using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class MagnetChainResolver
    {
        public static IReadOnlyList<DiceController> Collect(
            DiceController origin,
            Direction moveDirection,
            DiceRegistry registry) {
            if (origin == null || registry == null) {
                return System.Array.Empty<DiceController>();
            }

            if (!origin.Capabilities.HasMagnetCoupling) {
                return new[] { origin };
            }

            var chain = new List<DiceController> { origin };
            var tier = origin.CurrentState.Tier;
            var perpendicularDirections = GetPerpendicularDirections(moveDirection);

            foreach (var direction in perpendicularDirections) {
                CollectInDirection(origin.CurrentState.GridPos, direction, tier, registry, chain);
            }

            return chain;
        }

        static void CollectInDirection(
            UnityEngine.Vector2Int startCell,
            Direction direction,
            DiceStackTier tier,
            DiceRegistry registry,
            List<DiceController> chain) {
            var cell = startCell + direction.ToGridDelta();

            while (registry.TryGetDiceAt(cell, tier, out var dice)
                && dice != null
                && dice.Capabilities.HasMagnetCoupling
                && !ContainsDice(chain, dice)) {
                chain.Add(dice);
                cell += direction.ToGridDelta();
            }
        }

        static bool ContainsDice(List<DiceController> chain, DiceController dice) {
            for (var i = 0; i < chain.Count; i++) {
                if (chain[i] == dice) {
                    return true;
                }
            }

            return false;
        }

        static Direction[] GetPerpendicularDirections(Direction moveDirection) {
            return moveDirection switch {
                Direction.East or Direction.West => new[] { Direction.North, Direction.South },
                _ => new[] { Direction.East, Direction.West }
            };
        }
    }
}
