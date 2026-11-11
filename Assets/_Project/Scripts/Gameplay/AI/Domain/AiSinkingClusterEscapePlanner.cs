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
        /// Proactive sink escape is disabled. Standing on a sink-erasing cluster must not
        /// preempt match goals (MountAdjacent / forced descend caused leave↔mount loops).
        /// After an actual floor fall, FloorRecovery handles remount.
        /// </summary>
        public static AiSinkingClusterEscapeKind ResolveEscape(
            GameStateSnapshot snapshot,
            AiPlayerSettings settings,
            out int clusterFace,
            out List<DiceSnapshot> sinkingGroup,
            out DiceController mountTarget,
            MovementTransitionEvaluator passability = null,
            AiNavigationState? navigationState = null,
            float footingWorldY = 0f,
            PlayerSlot movementOwner = default,
            int maxSearchSteps = 64) {
            clusterFace = 0;
            sinkingGroup = null;
            mountTarget = null;
            return AiSinkingClusterEscapeKind.None;
        }

        public static bool IsTrappedOnSinkingCluster(
            GameStateSnapshot snapshot,
            AiPlayerSettings settings,
            out int trappedFace,
            out List<DiceSnapshot> sinkingGroup,
            MovementTransitionEvaluator passability = null,
            AiNavigationState? navigationState = null,
            float footingWorldY = 0f,
            PlayerSlot movementOwner = default,
            int maxSearchSteps = 64) {
            var kind = ResolveEscape(
                snapshot,
                settings,
                out trappedFace,
                out sinkingGroup,
                out _,
                passability,
                navigationState,
                footingWorldY,
                movementOwner,
                maxSearchSteps);
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
