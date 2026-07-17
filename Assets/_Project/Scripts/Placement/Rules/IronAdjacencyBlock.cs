using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Board query for magnet-blocking adjacency. Movable/couple resolution lives in
    /// <see cref="DiceEffectiveBehaviorResolver"/>.
    /// </summary>
    public static class IronAdjacencyBlock
    {
        static readonly Direction[] CardinalDirections = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static bool IsPlayerMovable(DiceController dice, DiceRegistry registry) {
            return DiceEffectiveBehaviorFactory.For(dice, registry).IsPlayerMovable;
        }

        public static bool CanJumpCoupleWithPlayer(DiceController dice, DiceRegistry registry) {
            if (dice == null) {
                return true;
            }

            return DiceEffectiveBehaviorFactory.For(dice, registry).CanJumpCoupleWithPlayer;
        }

        public static bool HasAdjacentMagnetBlocker(DiceController dice, DiceRegistry registry) {
            if (dice == null || registry == null) {
                return false;
            }

            var tier = dice.CurrentState.Tier;
            var cell = dice.CurrentState.GridPos;

            foreach (var direction in CardinalDirections) {
                var neighborCell = cell + direction.ToGridDelta();
                if (!registry.TryGetDiceAt(neighborCell, tier, out var neighborDice) || neighborDice == null) {
                    continue;
                }

                if (neighborDice.Capabilities.BlocksAdjacentMagnet
                    && !neighborDice.IsErasing
                    && !neighborDice.IsVanishing) {
                    return true;
                }
            }

            return false;
        }
    }
}
