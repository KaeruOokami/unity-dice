using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class WorkDieRollPathPlanner
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        const int MaxOrientSearchDepth = 16;

        public static bool TryFindOrientPath(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            int targetFace,
            bool allowJump,
            out List<Direction> directions) {
            directions = null;
            if (passability == null || workDie == null || startState.Orientation.Top == targetFace) {
                return false;
            }

            var startKey = BuildStateKey(startState);
            var visited = new HashSet<(int, int, int, int, int, int)> { startKey };
            var queue = new Queue<(DiceState state, int level, List<Direction> path)>();
            queue.Enqueue((startState, fromLevel, new List<Direction>()));

            while (queue.Count > 0) {
                var current = queue.Dequeue();
                if (current.state.Orientation.Top == targetFace) {
                    directions = current.path;
                    return directions.Count > 0;
                }

                if (current.path.Count >= MaxOrientSearchDepth) {
                    continue;
                }

                for (var i = 0; i < Directions.Length; i++) {
                    var direction = Directions[i];
                    if (!TrySimulateRollStep(
                        passability,
                        workDie,
                        current.state,
                        current.level,
                        footingWorldY,
                        movementOwner,
                        direction,
                        allowJump,
                        out var landingState,
                        out var landingLevel)) {
                        continue;
                    }

                    var key = BuildStateKey(landingState);
                    if (!visited.Add(key)) {
                        continue;
                    }

                    var nextPath = new List<Direction>(current.path) { direction };
                    queue.Enqueue((landingState, landingLevel, nextPath));
                }
            }

            return false;
        }

        public static bool TryValidateDirectionSequence(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            IReadOnlyList<Direction> directions,
            bool allowJump,
            Vector2Int? requiredEndCell = null,
            int? requiredEndTop = null) {
            if (passability == null || workDie == null || directions == null) {
                return false;
            }

            var state = startState;
            var level = fromLevel;
            for (var i = 0; i < directions.Count; i++) {
                if (!TrySimulateRollStep(
                    passability,
                    workDie,
                    state,
                    level,
                    footingWorldY,
                    movementOwner,
                    directions[i],
                    allowJump,
                    out state,
                    out level)) {
                    return false;
                }
            }

            if (requiredEndCell.HasValue && state.GridPos != requiredEndCell.Value) {
                return false;
            }

            if (requiredEndTop.HasValue && state.Orientation.Top != requiredEndTop.Value) {
                return false;
            }

            return true;
        }

        public static bool TrySimulateRollStep(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            DiceState fromState,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            Direction direction,
            bool allowJump,
            out DiceState landingState,
            out int landingLevel) {
            landingState = fromState;
            landingLevel = fromLevel;
            if (passability == null || workDie == null) {
                return false;
            }

            var groundContext = PassabilityContext.ForGround(footingWorldY, movementOwner);
            if (passability.TryBuildGridMovePlan(
                fromState,
                direction,
                1,
                groundContext,
                out var groundPlan,
                out _)) {
                landingState = CreateLandingState(fromState, groundPlan);
                landingLevel = SurfaceHeightLevel.FromDiceStackTier(groundPlan.To.Tier);
                return true;
            }

            if (!allowJump || !workDie.CanJumpCoupleWithPlayer) {
                return false;
            }

            var jumpContext = PassabilityContext.Jump(true, true, footingWorldY, movementOwner);
            if (!passability.TryBuildGridMovePlan(
                fromState,
                direction,
                1,
                jumpContext,
                out var jumpPlan,
                out _)) {
                return false;
            }

            landingState = CreateLandingState(fromState, jumpPlan);
            landingLevel = SurfaceHeightLevel.FromDiceStackTier(jumpPlan.To.Tier);
            return true;
        }

        static DiceState CreateLandingState(DiceState fromState, DiceGridMovePlan plan) {
            return new DiceState(
                plan.To.GridPos,
                fromState.Orientation.Roll(GetDirectionBetween(fromState.GridPos, plan.To.GridPos)),
                plan.To.Tier,
                fromState.Kind);
        }

        static Direction GetDirectionBetween(Vector2Int fromCell, Vector2Int toCell) {
            var delta = toCell - fromCell;
            if (delta.x > 0) {
                return Direction.East;
            }

            if (delta.x < 0) {
                return Direction.West;
            }

            if (delta.y > 0) {
                return Direction.North;
            }

            return Direction.South;
        }

        static (int, int, int, int, int, int) BuildStateKey(DiceState state) {
            return (
                state.GridPos.x,
                state.GridPos.y,
                (int)state.Tier,
                state.Orientation.Top,
                state.Orientation.North,
                state.Orientation.East);
        }
    }
}
