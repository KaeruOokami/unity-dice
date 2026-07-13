using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
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
            DiceRegistry registry,
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

                if (!CarryPlacementPassability.TryResolveTarget(cell, registry, out var tier, out _)) {
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
            Vector2Int fromCell,
            Vector2Int targetCell,
            DiceOrientation orientation,
            out WorkDieSlidePlan plan) {
            plan = default;
            if (!TryBuildSlideDirections(fromCell, targetCell, orientation, out var directions)) {
                return false;
            }

            plan = new WorkDieSlidePlan(fromCell, orientation, directions);
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
            Vector2Int fromCell,
            Vector2Int targetCell,
            DiceOrientation orientation,
            out List<Direction> directions) {
            directions = null;
            if (fromCell == targetCell) {
                directions = new List<Direction>();
                return true;
            }

            var delta = targetCell - fromCell;
            if (delta.x != 0 && delta.y != 0) {
                if (TryBuildSlideDirectionsAxisOrder(fromCell, targetCell, orientation, axisXFirst: true, out directions)) {
                    return true;
                }

                return TryBuildSlideDirectionsAxisOrder(fromCell, targetCell, orientation, axisXFirst: false, out directions);
            }

            return TryBuildSlideDirectionsAxisOrder(fromCell, targetCell, orientation, axisXFirst: delta.x != 0, out directions);
        }

        static bool TryBuildSlideDirectionsAxisOrder(
            Vector2Int fromCell,
            Vector2Int targetCell,
            DiceOrientation orientation,
            bool axisXFirst,
            out List<Direction> directions) {
            directions = new List<Direction>();
            var delta = targetCell - fromCell;
            var currentOrientation = orientation;

            if (axisXFirst) {
                if (!TryAppendAxisSlide(delta.x, true, currentOrientation, directions, out currentOrientation)) {
                    return false;
                }

                if (!TryAppendAxisSlide(delta.y, false, currentOrientation, directions, out currentOrientation)) {
                    return false;
                }
            } else {
                if (!TryAppendAxisSlide(delta.y, false, currentOrientation, directions, out currentOrientation)) {
                    return false;
                }

                if (!TryAppendAxisSlide(delta.x, true, currentOrientation, directions, out currentOrientation)) {
                    return false;
                }
            }

            return currentOrientation.Top == orientation.Top;
        }

        static bool TryAppendAxisSlide(
            int deltaComponent,
            bool isXAxis,
            DiceOrientation orientation,
            List<Direction> directions,
            out DiceOrientation resultOrientation) {
            resultOrientation = orientation;
            if (deltaComponent == 0) {
                return true;
            }

            var axisDirection = ResolveAxisDirection(deltaComponent, isXAxis);
            var steps = Mathf.Abs(deltaComponent);
            return TryExpandAxisSlide(orientation, axisDirection, steps, directions, out resultOrientation);
        }

        static Direction ResolveAxisDirection(int deltaComponent, bool isXAxis) {
            if (isXAxis) {
                return deltaComponent > 0 ? Direction.East : Direction.West;
            }

            return deltaComponent > 0 ? Direction.North : Direction.South;
        }

        static bool TryExpandAxisSlide(
            DiceOrientation orientation,
            Direction axisDirection,
            int steps,
            List<Direction> directions,
            out DiceOrientation resultOrientation) {
            resultOrientation = orientation;
            if (steps <= 0) {
                return true;
            }

            foreach (var perpendicular in GetPerpendicularDirections(axisDirection)) {
                var segment = new List<Direction>();
                var simulated = orientation;

                segment.Add(perpendicular);
                simulated = simulated.Roll(perpendicular);

                for (var i = 0; i < steps; i++) {
                    segment.Add(axisDirection);
                    simulated = simulated.Roll(axisDirection);
                }

                var closing = GetOppositeDirection(perpendicular);
                segment.Add(closing);
                simulated = simulated.Roll(closing);

                if (simulated.Top != orientation.Top) {
                    continue;
                }

                directions.AddRange(segment);
                resultOrientation = simulated;
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
