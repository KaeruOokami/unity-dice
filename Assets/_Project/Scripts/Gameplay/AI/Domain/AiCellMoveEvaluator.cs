using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct AdjacentCellCandidate
    {
        public Vector2Int Cell { get; }
        public Direction Direction { get; }
        public int PathLength { get; }
        public float Score { get; }
        public MovementTransitionKind EdgeKind { get; }

        public AdjacentCellCandidate(
            Vector2Int cell,
            Direction direction,
            int pathLength,
            float score,
            MovementTransitionKind edgeKind) {
            Cell = cell;
            Direction = direction;
            PathLength = pathLength;
            Score = score;
            EdgeKind = edgeKind;
        }
    }

    public static class AiCellMoveEvaluator
    {
        public static bool TrySelectBestAdjacentCell(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            Vector2Int goalCell,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceController standOnDie,
            int maxSearchSteps,
            AiNavigationConstraints constraints,
            out AdjacentCellCandidate best,
            out string candidateLog) {
            best = default;
            candidateLog = string.Empty;

            if (AiCellPathfinder.TryFindFirstStep(
                passability,
                start,
                goalCell,
                footingWorldY,
                movementOwner,
                maxSearchSteps,
                constraints,
                out var pathStep,
                out var pathLog,
                standOnDie)
                && !AiCellPathfinder.IsCanRollIncompatibleWithStandOnDie(
                    standOnDie,
                    start.StandingDice,
                    pathStep.EdgeKind,
                    pathStep.NextCell)) {
                var score = ScorePathStep(start.Cell, pathStep, goalCell, standOnDie);
                best = new AdjacentCellCandidate(
                    pathStep.NextCell,
                    pathStep.Direction,
                    pathStep.PathLength,
                    score,
                    pathStep.EdgeKind);
                candidateLog = $"path [{pathLog}]";
                return true;
            }

            if (AiCellPathfinder.TrySelectBestNavigableNeighbor(
                passability,
                start,
                goalCell,
                footingWorldY,
                movementOwner,
                standOnDie,
                constraints,
                out var fallbackStep,
                out var fallbackLog)) {
                var score = ScorePathStep(start.Cell, fallbackStep, goalCell, standOnDie);
                if (score >= 0f) {
                    best = new AdjacentCellCandidate(
                        fallbackStep.NextCell,
                        fallbackStep.Direction,
                        fallbackStep.PathLength,
                        score,
                        fallbackStep.EdgeKind);
                    candidateLog = $"fallback no-path [{pathLog}] neighbors=[{fallbackLog}]";
                    return true;
                }

                candidateLog = $"fallback-rejected score={score:F1} [{pathLog}] neighbors=[{fallbackLog}]";
                return false;
            }

            candidateLog = $"no-navigable-neighbors [{pathLog}]";
            return false;
        }

        static float ScorePathStep(
            Vector2Int fromCell,
            AiCellPathStep step,
            Vector2Int goalCell,
            DiceController standOnDie) {
            var currentDistance = DiceBoardAnalyzer.ManhattanDistance(fromCell, goalCell);
            var nextDistance = DiceBoardAnalyzer.ManhattanDistance(step.NextCell, goalCell);
            var score = (currentDistance - nextDistance) * 10f - step.PathLength;

            if (step.EdgeKind == MovementTransitionKind.CanRoll) {
                score -= 1f;
            }

            if (standOnDie != null
                && step.EdgeKind == MovementTransitionKind.Walkable
                && step.NextCell == standOnDie.CurrentState.GridPos) {
                score += 20f;
            }

            return score;
        }

        public static bool TryGetDirectionBetweenCells(Vector2Int fromCell, Vector2Int toCell, out Direction direction) {
            var delta = toCell - fromCell;
            if (delta.x == 1 && delta.y == 0) {
                direction = Direction.East;
                return true;
            }

            if (delta.x == -1 && delta.y == 0) {
                direction = Direction.West;
                return true;
            }

            if (delta.x == 0 && delta.y == 1) {
                direction = Direction.North;
                return true;
            }

            if (delta.x == 0 && delta.y == -1) {
                direction = Direction.South;
                return true;
            }

            direction = default;
            return false;
        }
    }
}
