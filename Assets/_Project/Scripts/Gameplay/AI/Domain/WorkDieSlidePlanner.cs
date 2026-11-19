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
        /// <summary>
        /// Join succeeds when the work die shares the goal face and is match-adjacent
        /// to any live member of the target cluster (planned landing cell is irrelevant).
        /// </summary>
        public static bool IsJoinComplete(
            DiceState state,
            IReadOnlyList<DiceSnapshot> cluster,
            int targetFace) {
            if (cluster == null || cluster.Count == 0) {
                return false;
            }

            if (state.Orientation.Top != targetFace) {
                return false;
            }

            var workSlot = new DiceSlot(state.GridPos, state.Tier);
            for (var i = 0; i < cluster.Count; i++) {
                var controller = cluster[i].Controller;
                if (controller == null) {
                    continue;
                }

                var memberState = controller.CurrentState;
                if (memberState.Orientation.Top != targetFace) {
                    continue;
                }

                var memberSlot = new DiceSlot(memberState.GridPos, memberState.Tier);
                if (DiceStackAdjacency.IsAdjacentForMatch(workSlot, memberSlot)) {
                    return true;
                }
            }

            return false;
        }

        public static bool IsJoinLandingAvailable(
            DiceRegistry registry,
            DiceController workDie,
            Vector2Int targetCell,
            DiceStackTier targetTier) {
            if (registry == null || workDie == null) {
                return false;
            }

            var state = workDie.CurrentState;
            if (state.GridPos == targetCell && state.Tier == targetTier) {
                return true;
            }

            return CarryPlacementPassability.CanPlaceAt(targetCell, targetTier, registry, out _);
        }

        public static bool CanWorkDieExtendCluster(
            IReadOnlyList<DiceSnapshot> cluster,
            DiceSnapshot workDie,
            IReadOnlyList<DiceSnapshot> allDice,
            DiceRegistry registry,
            VersusArenaLayout versusLayout,
            PlayerSlot playerSlot) {
            return TrySelectJoinTargetCell(
                cluster,
                workDie,
                allDice,
                registry,
                versusLayout,
                playerSlot,
                out _,
                out _);
        }

        public static bool TrySelectJoinTargetCell(
            IReadOnlyList<DiceSnapshot> cluster,
            DiceSnapshot workDie,
            IReadOnlyList<DiceSnapshot> allDice,
            DiceRegistry registry,
            VersusArenaLayout versusLayout,
            PlayerSlot playerSlot,
            out Vector2Int targetCell,
            out DiceStackTier targetTier,
            Vector2Int? excludeCell = null,
            DiceStackTier? excludeTier = null) {
            HashSet<long> excludeKeys = null;
            if (excludeCell.HasValue && excludeTier.HasValue) {
                excludeKeys = new HashSet<long> {
                    JoinSlotKey(excludeCell.Value, excludeTier.Value)
                };
            }

            return TrySelectJoinTargetCell(
                cluster,
                workDie,
                allDice,
                registry,
                versusLayout,
                playerSlot,
                out targetCell,
                out targetTier,
                excludeKeys);
        }

        public static bool TrySelectJoinTargetCell(
            IReadOnlyList<DiceSnapshot> cluster,
            DiceSnapshot workDie,
            IReadOnlyList<DiceSnapshot> allDice,
            DiceRegistry registry,
            VersusArenaLayout versusLayout,
            PlayerSlot playerSlot,
            out Vector2Int targetCell,
            out DiceStackTier targetTier,
            ISet<long> excludeSlotKeys) {
            targetCell = default;
            targetTier = default;
            if (cluster == null || cluster.Count == 0 || registry == null || workDie.Controller == null) {
                return false;
            }

            // Join landings must match the cluster tier (same-tier expansion only).
            var clusterTier = cluster[0].Tier;
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

                if (!CarryPlacementPassability.CanPlaceAt(cell, clusterTier, registry, out _)) {
                    continue;
                }

                if (excludeSlotKeys != null
                    && excludeSlotKeys.Contains(JoinSlotKey(cell, clusterTier))) {
                    continue;
                }

                if (!IsJoinLandingAvailable(registry, workDie.Controller, cell, clusterTier)) {
                    continue;
                }

                if (!ClusterSelectionEvaluator.HasMovableExternalNeighbor(
                    cell,
                    clusterTier,
                    cluster,
                    allDice,
                    workDie.Controller)) {
                    continue;
                }

                // Already parked on a valid join slot.
                if (workDie.GridPos == cell && workDie.Tier == clusterTier) {
                    targetCell = cell;
                    targetTier = clusterTier;
                    return true;
                }

                var score = (float)(-DiceBoardAnalyzer.ManhattanDistance(workDie.GridPos, cell));
                if (score > bestScore) {
                    bestScore = score;
                    targetCell = cell;
                    targetTier = clusterTier;
                    found = true;
                }
            }

            return found;
        }

        public static long JoinSlotKey(Vector2Int cell, DiceStackTier tier) {
            return ((long)cell.x << 32) ^ ((long)(cell.y & 0xFFFF) << 16) ^ (long)tier;
        }

        public static bool TryBuildSlidePlan(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            int targetFace,
            IReadOnlyList<DiceSnapshot> cluster,
            bool allowJump,
            out WorkDieSlidePlan plan,
            out DiceState endState,
            WorkDieRollCrushAvoidance crushAvoidance = default,
            DiceRegistry registry = null) {
            plan = default;
            endState = startState;
            if (!WorkDieRollPathPlanner.TryFindJoinPath(
                passability,
                workDie,
                fromLevel,
                footingWorldY,
                movementOwner,
                startState,
                targetFace,
                cluster,
                allowJump,
                out var directions,
                out endState,
                crushAvoidance,
                registry)
                || directions == null) {
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
    }
}
