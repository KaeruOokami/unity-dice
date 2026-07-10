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
            var arm = new List<DiceController>();
            foreach (var direction in GetPerpendicularDirections(moveDirection)) {
                CollectArm(origin, direction, tier, registry, arm);
                chain.AddRange(arm);
            }

            return chain;
        }

        public static void CollectArm(
            DiceController origin,
            Direction armDirection,
            DiceStackTier tier,
            DiceRegistry registry,
            List<DiceController> arm) {
            arm.Clear();
            if (origin == null || registry == null) {
                return;
            }

            var cell = origin.CurrentState.GridPos + armDirection.ToGridDelta();
            while (registry.TryGetDiceAt(cell, tier, out var dice)
                && dice != null
                && dice.Capabilities.HasMagnetCoupling) {
                arm.Add(dice);
                cell += armDirection.ToGridDelta();
            }
        }

        public static Direction[] GetPerpendicularDirections(Direction moveDirection) {
            return moveDirection switch {
                Direction.East or Direction.West => new[] { Direction.North, Direction.South },
                _ => new[] { Direction.East, Direction.West }
            };
        }
    }
}
