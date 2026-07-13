using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct WorkDieSlidePlan
    {
        public Vector2Int StartCell { get; }
        public DiceOrientation StartOrientation { get; }
        public IReadOnlyList<Direction> Directions { get; }

        public WorkDieSlidePlan(
            Vector2Int startCell,
            DiceOrientation startOrientation,
            IReadOnlyList<Direction> directions) {
            StartCell = startCell;
            StartOrientation = startOrientation;
            Directions = directions;
        }
    }

    public static class WorkDieSlidePlanner
    {
        public static bool TrySelectJoinTargetCell(
            IReadOnlyList<DiceSnapshot> cluster,
            DiceSnapshot workDie,
            IReadOnlyList<DiceSnapshot> allDice,
            DiceRegistry registry,
            VersusArenaLayout versusLayout,
            PlayerSlot playerSlot,
            out Vector2Int targetCell,
            out DiceStackTier targetTier) {
            targetCell = default;
            targetTier = default;
            if (cluster == null || cluster.Count == 0 || registry == null || workDie.Controller == null) {
                return false;
            }

            var clusterCells = ClusterSelectionEvaluator.GetClusterCells(cluster);
            var adjacent = CollectClusterAdjacentCells(clusterCells);
            var bestScore = float.MinValue;
            var found = false;

            foreach (var cell in adjacent) {
                if (clusterCells.Contains(cell)) {
                    continue;
                }

                if (!AiRegionFilter.IsInPlayerRegion(versusLayout, playerSlot, cell)) {
                    continue;
                }

                if (!CarryPlacementPassability.TryResolveTarget(cell, registry, out var tier, out _)) {
                    continue;
                }

                if (!ClusterSelectionEvaluator.HasMovableExternalNeighbor(
                    cell,
                    tier,
                    cluster,
                    allDice,
                    workDie.Controller)) {
                    continue;
                }

                if (workDie.GridPos == cell) {
                    targetCell = cell;
                    targetTier = tier;
                    return true;
                }

                var score = -DiceBoardAnalyzer.ManhattanDistance(workDie.GridPos, cell);
                if (score > bestScore) {
                    bestScore = score;
                    targetCell = cell;
                    targetTier = tier;
                    found = true;
                }
            }

            return found;
        }

        public static bool TryBuildSlidePlan(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            Vector2Int targetCell,
            bool allowJump,
            out WorkDieSlidePlan plan) {
            plan = default;
            if (!TryBuildSlideDirections(
                passability,
                workDie,
                fromLevel,
                footingWorldY,
                movementOwner,
                startState,
                targetCell,
                allowJump,
                out var directions)) {
                return false;
            }

            plan = new WorkDieSlidePlan(startState.GridPos, startState.Orientation, directions);
            return true;
        }

        public static bool TryAdvanceCompletedSteps(WorkDieSlidePlan plan, ref int stepIndex, DiceState state) {
            if (plan.Directions == null) {
                return false;
            }

            var advanced = false;
            while (stepIndex < plan.Directions.Count
                && TrySimulateAfterSteps(plan, stepIndex + 1, out var cell, out var orientation)
                && cell == state.GridPos
                && orientation.Top == state.Orientation.Top
                && orientation.North == state.Orientation.North
                && orientation.East == state.Orientation.East) {
                stepIndex++;
                advanced = true;
            }

            return advanced;
        }

        public static bool TrySimulateAfterSteps(
            WorkDieSlidePlan plan,
            int completedStepCount,
            out Vector2Int cell,
            out DiceOrientation orientation) {
            cell = plan.StartCell;
            orientation = plan.StartOrientation;
            if (completedStepCount < 0 || plan.Directions == null) {
                return false;
            }

            if (completedStepCount > plan.Directions.Count) {
                return false;
            }

            for (var i = 0; i < completedStepCount; i++) {
                orientation = orientation.Roll(plan.Directions[i]);
                cell += plan.Directions[i].ToGridDelta();
            }

            return true;
        }

        public static bool IsPlanStillValid(WorkDieSlidePlan plan, int stepIndex, DiceState state) {
            if (plan.Directions == null) {
                return false;
            }

            if (stepIndex == 0) {
                return state.GridPos == plan.StartCell
                    && state.Orientation.Top == plan.StartOrientation.Top
                    && state.Orientation.North == plan.StartOrientation.North
                    && state.Orientation.East == plan.StartOrientation.East;
            }

            return TrySimulateAfterSteps(plan, stepIndex, out var cell, out var orientation)
                && cell == state.GridPos
                && orientation.Top == state.Orientation.Top
                && orientation.North == state.Orientation.North
                && orientation.East == state.Orientation.East;
        }

        public static bool TrySelectNextStep(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            WorkDieSlidePlan plan,
            int stepIndex,
            bool allowJump,
            out WorkDieRollStep step) {
            step = default;
            if (passability == null || workDie == null || !workDie.Capabilities.CanGridRoll) {
                return false;
            }

            if (plan.Directions == null || stepIndex < 0 || stepIndex >= plan.Directions.Count) {
                return false;
            }

            return WorkDieRollPlanner.TrySelectRollStep(
                passability,
                workDie,
                fromLevel,
                footingWorldY,
                movementOwner,
                plan.Directions[stepIndex],
                allowJump,
                out step);
        }

        static HashSet<Vector2Int> CollectClusterAdjacentCells(HashSet<Vector2Int> clusterCells) {
            var adjacent = new HashSet<Vector2Int>();
            foreach (var cell in clusterCells) {
                foreach (var neighbor in DiceBoardAnalyzer.GetAdjacentCells(cell)) {
                    if (!clusterCells.Contains(neighbor)) {
                        adjacent.Add(neighbor);
                    }
                }
            }

            return adjacent;
        }

        static bool TryBuildSlideDirections(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            Vector2Int targetCell,
            bool allowJump,
            out List<Direction> directions) {
            directions = null;
            if (startState.GridPos == targetCell) {
                directions = new List<Direction>();
                return true;
            }

            var delta = targetCell - startState.GridPos;
            if (delta.x != 0 && delta.y != 0) {
                if (TryBuildSlideDirectionsAxisOrder(
                    passability,
                    workDie,
                    fromLevel,
                    footingWorldY,
                    movementOwner,
                    startState,
                    targetCell,
                    axisXFirst: true,
                    allowJump,
                    out directions)) {
                    return true;
                }

                return TryBuildSlideDirectionsAxisOrder(
                    passability,
                    workDie,
                    fromLevel,
                    footingWorldY,
                    movementOwner,
                    startState,
                    targetCell,
                    axisXFirst: false,
                    allowJump,
                    out directions);
            }

            return TryBuildSlideDirectionsAxisOrder(
                passability,
                workDie,
                fromLevel,
                footingWorldY,
                movementOwner,
                startState,
                targetCell,
                axisXFirst: delta.x != 0,
                allowJump,
                out directions);
        }

        static bool TryBuildSlideDirectionsAxisOrder(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            Vector2Int targetCell,
            bool axisXFirst,
            bool allowJump,
            out List<Direction> directions) {
            directions = new List<Direction>();
            var delta = targetCell - startState.GridPos;
            var currentState = startState;
            var currentLevel = fromLevel;

            if (axisXFirst) {
                if (!TryAppendAxisSlide(
                    passability,
                    workDie,
                    currentLevel,
                    footingWorldY,
                    movementOwner,
                    delta.x,
                    true,
                    currentState,
                    directions,
                    allowJump,
                    out currentState,
                    out currentLevel)) {
                    return false;
                }

                if (!TryAppendAxisSlide(
                    passability,
                    workDie,
                    currentLevel,
                    footingWorldY,
                    movementOwner,
                    delta.y,
                    false,
                    currentState,
                    directions,
                    allowJump,
                    out currentState,
                    out currentLevel)) {
                    return false;
                }
            } else {
                if (!TryAppendAxisSlide(
                    passability,
                    workDie,
                    currentLevel,
                    footingWorldY,
                    movementOwner,
                    delta.y,
                    false,
                    currentState,
                    directions,
                    allowJump,
                    out currentState,
                    out currentLevel)) {
                    return false;
                }

                if (!TryAppendAxisSlide(
                    passability,
                    workDie,
                    currentLevel,
                    footingWorldY,
                    movementOwner,
                    delta.x,
                    true,
                    currentState,
                    directions,
                    allowJump,
                    out currentState,
                    out currentLevel)) {
                    return false;
                }
            }

            return currentState.Orientation.Top == startState.Orientation.Top
                && currentState.GridPos == targetCell;
        }

        static bool TryAppendAxisSlide(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            int deltaComponent,
            bool isXAxis,
            DiceState startState,
            List<Direction> directions,
            bool allowJump,
            out DiceState resultState,
            out int resultLevel) {
            resultState = startState;
            resultLevel = fromLevel;
            if (deltaComponent == 0) {
                return true;
            }

            var axisDirection = ResolveAxisDirection(deltaComponent, isXAxis);
            var steps = Mathf.Abs(deltaComponent);
            return TryExpandAxisSlide(
                passability,
                workDie,
                fromLevel,
                footingWorldY,
                movementOwner,
                startState,
                axisDirection,
                steps,
                directions,
                allowJump,
                out resultState,
                out resultLevel);
        }

        static Direction ResolveAxisDirection(int deltaComponent, bool isXAxis) {
            if (isXAxis) {
                return deltaComponent > 0 ? Direction.East : Direction.West;
            }

            return deltaComponent > 0 ? Direction.North : Direction.South;
        }

        static bool TryExpandAxisSlide(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            Direction axisDirection,
            int steps,
            List<Direction> directions,
            bool allowJump,
            out DiceState resultState,
            out int resultLevel) {
            resultState = startState;
            resultLevel = fromLevel;
            if (steps <= 0) {
                return true;
            }

            foreach (var perpendicular in GetPerpendicularDirections(axisDirection)) {
                var segment = new List<Direction>();
                var simulated = startState;

                segment.Add(perpendicular);
                if (!WorkDieRollPathPlanner.TrySimulateRollStep(
                    passability,
                    workDie,
                    simulated,
                    fromLevel,
                    footingWorldY,
                    movementOwner,
                    perpendicular,
                    allowJump,
                    out simulated,
                    out var segmentLevel)) {
                    continue;
                }

                for (var i = 0; i < steps; i++) {
                    segment.Add(axisDirection);
                    if (!WorkDieRollPathPlanner.TrySimulateRollStep(
                        passability,
                        workDie,
                        simulated,
                        segmentLevel,
                        footingWorldY,
                        movementOwner,
                        axisDirection,
                        allowJump,
                        out simulated,
                        out segmentLevel)) {
                        segment = null;
                        break;
                    }
                }

                if (segment == null) {
                    continue;
                }

                var closing = GetOppositeDirection(perpendicular);
                segment.Add(closing);
                if (!WorkDieRollPathPlanner.TrySimulateRollStep(
                    passability,
                    workDie,
                    simulated,
                    segmentLevel,
                    footingWorldY,
                    movementOwner,
                    closing,
                    allowJump,
                    out simulated,
                    out segmentLevel)) {
                    continue;
                }

                if (simulated.Orientation.Top != startState.Orientation.Top) {
                    continue;
                }

                directions.AddRange(segment);
                resultState = simulated;
                resultLevel = segmentLevel;
                return true;
            }

            return false;
        }

        static IEnumerable<Direction> GetPerpendicularDirections(Direction axisDirection) {
            if (axisDirection == Direction.East || axisDirection == Direction.West) {
                yield return Direction.North;
                yield return Direction.South;
                yield break;
            }

            yield return Direction.East;
            yield return Direction.West;
        }

        static Direction GetOppositeDirection(Direction direction) {
            return direction switch {
                Direction.East => Direction.West,
                Direction.West => Direction.East,
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                _ => direction
            };
        }
    }
}
