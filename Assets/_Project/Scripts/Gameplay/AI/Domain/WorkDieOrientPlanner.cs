using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class WorkDieOrientPlanner
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static bool TrySelectNextStep(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            int targetFace,
            bool allowJump,
            out WorkDieRollStep step,
            out int remainingRolls) {
            step = default;
            remainingRolls = int.MaxValue;
            if (passability == null || workDie == null) {
                return false;
            }

            var state = workDie.CurrentState;
            if (state.Orientation.Top == targetFace) {
                return false;
            }

            if (!workDie.Capabilities.CanGridRoll) {
                return false;
            }

            var orderedDirections = OrderDirectionsByOrientProgress(state.Orientation, targetFace);
            for (var i = 0; i < orderedDirections.Count; i++) {
                var direction = orderedDirections[i];
                var rollsAfterStep = CountRollsToTarget(state.Orientation.Roll(direction), targetFace);
                if (!WorkDieRollPlanner.TrySelectRollStep(
                    passability,
                    workDie,
                    fromLevel,
                    footingWorldY,
                    movementOwner,
                    direction,
                    allowJump,
                    out step)) {
                    continue;
                }

                remainingRolls = rollsAfterStep;
                return true;
            }

            return false;
        }

        static List<Direction> OrderDirectionsByOrientProgress(DiceOrientation from, int targetFace) {
            var ranked = new List<(Direction direction, int rollsToTarget)>(Directions.Length);
            for (var i = 0; i < Directions.Length; i++) {
                var direction = Directions[i];
                var rollsToTarget = CountRollsToTarget(from.Roll(direction), targetFace);
                ranked.Add((direction, rollsToTarget));
            }

            ranked.Sort((a, b) => a.rollsToTarget.CompareTo(b.rollsToTarget));
            var ordered = new List<Direction>(ranked.Count);
            for (var i = 0; i < ranked.Count; i++) {
                ordered.Add(ranked[i].direction);
            }

            return ordered;
        }

        static int CountRollsToTarget(DiceOrientation from, int targetFace) {
            if (from.Top == targetFace) {
                return 0;
            }

            var visited = new HashSet<(int, int, int)>();
            var queue = new Queue<(DiceOrientation orientation, int depth)>();
            queue.Enqueue((from, 0));

            while (queue.Count > 0) {
                var current = queue.Dequeue();
                var key = (current.orientation.Top, current.orientation.North, current.orientation.East);
                if (!visited.Add(key)) {
                    continue;
                }

                if (current.orientation.Top == targetFace) {
                    return current.depth;
                }

                for (var i = 0; i < Directions.Length; i++) {
                    queue.Enqueue((current.orientation.Roll(Directions[i]), current.depth + 1));
                }
            }

            return int.MaxValue;
        }
    }
}
