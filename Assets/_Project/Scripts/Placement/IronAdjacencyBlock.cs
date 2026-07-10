using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class IronAdjacencyBlock
    {
        static readonly Direction[] CardinalDirections = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static bool IsPlayerMovable(DiceController dice, DiceRegistry registry) {
            if (dice == null || registry == null) {
                return false;
            }

            var capabilities = dice.Capabilities;
            if (!capabilities.CanBePushedByPlayer && !capabilities.CanGridRoll && !capabilities.SlideUntilBlocked) {
                return false;
            }

            if (!capabilities.HasMagnetCoupling) {
                return capabilities.CanBePushedByPlayer || capabilities.CanGridRoll || capabilities.SlideUntilBlocked;
            }

            return !HasAdjacentIron(dice, registry);
        }

        public static bool CanJumpCoupleWithPlayer(DiceController dice, DiceRegistry registry) {
            if (dice == null) {
                return true;
            }

            if (!dice.Capabilities.CanJumpCoupleWithPlayer) {
                return false;
            }

            if (dice.Capabilities.HasMagnetCoupling && HasAdjacentIron(dice, registry)) {
                return false;
            }

            return true;
        }

        static bool HasAdjacentIron(DiceController dice, DiceRegistry registry) {
            var tier = dice.CurrentState.Tier;
            var cell = dice.CurrentState.GridPos;

            foreach (var direction in CardinalDirections) {
                var neighborCell = cell + direction.ToGridDelta();
                if (!registry.TryGetDiceAt(neighborCell, tier, out var neighborDice) || neighborDice == null) {
                    continue;
                }

                if (neighborDice.Kind == DiceKind.Iron
                    && !neighborDice.IsDissolving
                    && !neighborDice.IsVanishing) {
                    return true;
                }
            }

            return false;
        }
    }
}
