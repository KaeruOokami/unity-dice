using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public enum AiSinkingClusterEscapeKind
    {
        None,
        MountAdjacent,
        DescendToFloor
    }

    public static class AiSinkingClusterEscapePlanner
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        /// <summary>
        /// While standing on a sink-erasing cluster:
        /// adjacent external die → mount it for chain; otherwise descend immediately.
        /// </summary>
        public static AiSinkingClusterEscapeKind ResolveEscape(
            GameStateSnapshot snapshot,
            AiPlayerSettings settings,
            out int clusterFace,
            out List<DiceSnapshot> sinkingGroup,
            out DiceController mountTarget) {
            clusterFace = 0;
            sinkingGroup = null;
            mountTarget = null;

            var standing = snapshot?.StandingDice;
            if (standing == null || !standing.IsSinkErasing || settings == null) {
                return AiSinkingClusterEscapeKind.None;
            }

            clusterFace = standing.CurrentState.Orientation.Top;
            sinkingGroup = SinkingChainEvaluator.GetSinkingDice(clusterFace, snapshot.PlanningDice);
            if (sinkingGroup.Count == 0
                || !ClusterSelectionEvaluator.ClusterContainsController(sinkingGroup, standing)) {
                sinkingGroup = null;
                clusterFace = 0;
                return AiSinkingClusterEscapeKind.None;
            }

            if (AiFloorRecoveryPlanner.TrySelectNearestAdjacentExternalDie(
                sinkingGroup,
                clusterFace,
                snapshot.PlanningDice,
                snapshot.PlayerCell,
                out var adjacent)
                && adjacent.Controller != null
                && adjacent.Controller != standing) {
                mountTarget = adjacent.Controller;
                return AiSinkingClusterEscapeKind.MountAdjacent;
            }

            return AiSinkingClusterEscapeKind.DescendToFloor;
        }

        public static bool IsTrappedOnSinkingCluster(
            GameStateSnapshot snapshot,
            AiPlayerSettings settings,
            out int trappedFace,
            out List<DiceSnapshot> sinkingGroup) {
            var kind = ResolveEscape(snapshot, settings, out trappedFace, out sinkingGroup, out _);
            if (kind != AiSinkingClusterEscapeKind.DescendToFloor) {
                sinkingGroup = null;
                trappedFace = 0;
                return false;
            }

            return true;
        }

        public static bool NeedsDescent(GameStateSnapshot snapshot) {
            return snapshot?.StandingDice != null && snapshot.StandingDice.IsSinkErasing;
        }

        public static bool TrySelectDescentStep(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            float footingWorldY,
            PlayerSlot player,
            out Direction direction,
            out Vector2Int stepCell,
            out MovementTransitionKind edgeKind) {
            direction = default;
            stepCell = default;
            edgeKind = default;

            if (passability == null) {
                return false;
            }

            var context = PassabilityContext.ForGround(footingWorldY, player);
            var found = false;
            var bestPriority = int.MinValue;

            for (var i = 0; i < Directions.Length; i++) {
                var candidateDirection = Directions[i];
                var transition = passability.Evaluate(
                    start.Cell,
                    start.Level,
                    candidateDirection,
                    start.StandingDice,
                    context);

                var priority = ScoreDescentTransition(transition);
                if (priority < 0) {
                    continue;
                }

                var candidateStepCell = start.Cell + candidateDirection.ToGridDelta();
                if (priority > bestPriority) {
                    bestPriority = priority;
                    direction = candidateDirection;
                    stepCell = candidateStepCell;
                    edgeKind = transition.Kind;
                    found = true;
                }
            }

            return found;
        }

        static int ScoreDescentTransition(MovementTransition transition) {
            if (transition.IsDissolveDescentToFloor) {
                return 100;
            }

            if (transition.IsDissolveDescentHold) {
                return 80;
            }

            if (transition.Kind == MovementTransitionKind.Walkable
                && transition.TargetLevel == SurfaceHeightLevel.Floor) {
                return 60;
            }

            return -1;
        }
    }
}
