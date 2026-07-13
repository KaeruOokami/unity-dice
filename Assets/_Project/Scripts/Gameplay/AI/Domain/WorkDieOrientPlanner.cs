using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public enum WorkDieOrientExecutionMode
    {
        GroundRoll,
        JumpRoll
    }

    public readonly struct WorkDieOrientStep
    {
        public Direction Direction { get; }
        public Vector2Int LandingCell { get; }
        public DiceStackTier LandingTier { get; }
        public WorkDieOrientExecutionMode Mode { get; }
        public int RemainingRolls { get; }

        public WorkDieOrientStep(
            Direction direction,
            Vector2Int landingCell,
            DiceStackTier landingTier,
            WorkDieOrientExecutionMode mode,
            int remainingRolls) {
            Direction = direction;
            LandingCell = landingCell;
            LandingTier = landingTier;
            Mode = mode;
            RemainingRolls = remainingRolls;
        }
    }

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
            out WorkDieOrientStep step) {
            step = default;
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
            var fromCell = state.GridPos;
            var groundContext = PassabilityContext.ForGround(footingWorldY, movementOwner);
            var jumpContext = PassabilityContext.Jump(true, true, footingWorldY, movementOwner);

            for (var i = 0; i < orderedDirections.Count; i++) {
                var direction = orderedDirections[i];
                var landingCell = fromCell + direction.ToGridDelta();
                var remainingRolls = CountRollsToTarget(state.Orientation.Roll(direction), targetFace);

                var groundTransition = passability.Evaluate(
                    fromCell,
                    fromLevel,
                    direction,
                    workDie,
                    groundContext);

                if (TryResolveGroundRoll(groundTransition, landingCell, out var groundLandingTier)) {
                    step = new WorkDieOrientStep(
                        direction,
                        landingCell,
                        groundLandingTier,
                        WorkDieOrientExecutionMode.GroundRoll,
                        remainingRolls);
                    return true;
                }

                if (!allowJump || !workDie.CanJumpCoupleWithPlayer) {
                    continue;
                }

                if (groundTransition.Kind == MovementTransitionKind.CanRoll) {
                    continue;
                }

                var jumpTransition = passability.Evaluate(
                    fromCell,
                    fromLevel,
                    direction,
                    workDie,
                    jumpContext);

                if (TryResolveJumpRoll(jumpTransition, landingCell, out var jumpLandingTier)) {
                    step = new WorkDieOrientStep(
                        direction,
                        landingCell,
                        jumpLandingTier,
                        WorkDieOrientExecutionMode.JumpRoll,
                        remainingRolls);
                    return true;
                }
            }

            return false;
        }

        static bool TryResolveGroundRoll(
            MovementTransition transition,
            Vector2Int landingCell,
            out DiceStackTier landingTier) {
            landingTier = default;
            if (transition.Kind != MovementTransitionKind.CanRoll
                || !transition.HasDiceGridMovePlan
                || transition.DiceGridMovePlan.To.GridPos != landingCell) {
                return false;
            }

            landingTier = transition.DiceGridMovePlan.To.Tier;
            return true;
        }

        static bool TryResolveJumpRoll(
            MovementTransition transition,
            Vector2Int landingCell,
            out DiceStackTier landingTier) {
            landingTier = default;
            if (!transition.HasDiceGridMovePlan
                || transition.DiceGridMovePlan.To.GridPos != landingCell) {
                return false;
            }

            if (transition.Kind == MovementTransitionKind.CanRoll) {
                landingTier = transition.DiceGridMovePlan.To.Tier;
                return true;
            }

            if (transition.Kind == MovementTransitionKind.Walkable
                && transition.Route == MovementTransitionRoute.CoupledGridMove) {
                landingTier = transition.DiceGridMovePlan.To.Tier;
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
