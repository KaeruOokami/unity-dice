using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class AiSinkingClusterEscapePlanner
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static bool IsTrappedOnSinkingCluster(
            GameStateSnapshot snapshot,
            AiPlayerSettings settings,
            out int trappedFace,
            out List<DiceSnapshot> sinkingGroup) {
            trappedFace = 0;
            sinkingGroup = null;

            var standing = snapshot?.StandingDice;
            if (standing == null || !standing.IsSinkErasing || settings == null) {
                return false;
            }

            trappedFace = standing.CurrentState.Orientation.Top;
            sinkingGroup = SinkingChainEvaluator.GetSinkingDice(trappedFace, snapshot.PlanningDice);
            if (sinkingGroup.Count == 0
                || !ClusterSelectionEvaluator.ClusterContainsController(sinkingGroup, standing)) {
                sinkingGroup = null;
                return false;
            }

            if (AiFloorRecoveryPlanner.HasAdjacentClusterExternalDie(
                sinkingGroup,
                trappedFace,
                snapshot.PlanningDice)) {
                sinkingGroup = null;
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
