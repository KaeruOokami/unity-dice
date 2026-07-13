using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct AiCellPathStep
    {
        public Vector2Int NextCell { get; }
        public Direction Direction { get; }
        public int PathLength { get; }
        public MovementTransitionKind EdgeKind { get; }

        public AiCellPathStep(
            Vector2Int nextCell,
            Direction direction,
            int pathLength,
            MovementTransitionKind edgeKind) {
            NextCell = nextCell;
            Direction = direction;
            PathLength = pathLength;
            EdgeKind = edgeKind;
        }
    }

    public static class AiCellPathfinder
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        struct SearchNode
        {
            public AiNavigationState State;
            public Direction FirstDirection;
            public Vector2Int FirstCell;
            public int Depth;
            public MovementTransitionKind FirstEdgeKind;
        }

        public static bool TryFindFirstStep(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            Vector2Int goalCell,
            float footingWorldY,
            PlayerSlot movementOwner,
            int maxSearchSteps,
            AiNavigationConstraints constraints,
            out AiCellPathStep step,
            out string searchLog) {
            step = default;
            searchLog = string.Empty;

            if (passability == null) {
                searchLog = "passability-null";
                return false;
            }

            if (start.Cell == goalCell) {
                searchLog = "already-at-goal-cell";
                return false;
            }

            if (maxSearchSteps <= 0) {
                searchLog = "maxSearchSteps<=0";
                return false;
            }

            var context = PassabilityContext.ForGround(footingWorldY, movementOwner);
            var visited = new HashSet<AiNavigationState>();
            var queue = new Queue<SearchNode>();

            queue.Enqueue(new SearchNode {
                State = start,
                FirstDirection = default,
                FirstCell = default,
                Depth = 0,
                FirstEdgeKind = default
            });
            visited.Add(start);

            var expanded = 0;
            var log = string.Empty;

            while (queue.Count > 0) {
                var node = queue.Dequeue();
                if (node.Depth >= maxSearchSteps) {
                    continue;
                }

                for (var i = 0; i < Directions.Length; i++) {
                    var direction = Directions[i];
                    var transition = passability.Evaluate(
                        node.State.Cell,
                        node.State.Level,
                        direction,
                        node.State.StandingDice,
                        context);

                    if (!TryResolveNavigationEdge(
                        transition,
                        direction,
                        node.State,
                        out var neighborCell,
                        out var neighborState,
                        out var edgeKind)) {
                        continue;
                    }

                    if (!constraints.IsCellAllowed(neighborCell, goalCell)) {
                        log += $" {neighborCell}:forbidden";
                        continue;
                    }

                    if (!visited.Add(neighborState)) {
                        continue;
                    }

                    expanded++;
                    var firstDirection = node.Depth == 0 ? direction : node.FirstDirection;
                    var firstCell = node.Depth == 0 ? neighborCell : node.FirstCell;
                    var firstEdgeKind = node.Depth == 0 ? edgeKind : node.FirstEdgeKind;
                    var pathLength = node.Depth + 1;

                    if (neighborCell == goalCell) {
                        step = new AiCellPathStep(firstCell, firstDirection, pathLength, firstEdgeKind);
                        searchLog = $"path-found length={pathLength} expanded={expanded}";
                        return true;
                    }

                    queue.Enqueue(new SearchNode {
                        State = neighborState,
                        FirstDirection = firstDirection,
                        FirstCell = firstCell,
                        Depth = pathLength,
                        FirstEdgeKind = firstEdgeKind
                    });
                }
            }

            searchLog = $"path-not-found expanded={expanded}";
            return false;
        }

        public static bool TrySelectBestNavigableNeighbor(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            Vector2Int goalCell,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceController standOnDie,
            AiNavigationConstraints constraints,
            out AiCellPathStep step,
            out string candidateLog) {
            step = default;
            candidateLog = string.Empty;

            if (passability == null) {
                return false;
            }

            var context = PassabilityContext.ForGround(footingWorldY, movementOwner);
            var bestScore = float.MinValue;
            var found = false;
            var log = string.Empty;

            for (var i = 0; i < Directions.Length; i++) {
                var direction = Directions[i];
                var transition = passability.Evaluate(
                    start.Cell,
                    start.Level,
                    direction,
                    start.StandingDice,
                    context);

                if (!TryResolveNavigationEdge(
                    transition,
                    direction,
                    start,
                    out var neighborCell,
                    out _,
                    out var edgeKind)) {
                    log += $" {start.Cell + direction.ToGridDelta()}:blocked({transition.Kind})";
                    continue;
                }

                if (!constraints.IsCellAllowed(neighborCell, goalCell)) {
                    log += $" {neighborCell}:forbidden";
                    continue;
                }

                var score = ScoreNeighborCell(start.Cell, neighborCell, goalCell, standOnDie, edgeKind);
                log += $" {neighborCell}:{edgeKind} score={score:F1}";

                if (score > bestScore) {
                    bestScore = score;
                    step = new AiCellPathStep(neighborCell, direction, 1, edgeKind);
                    found = true;
                }
            }

            candidateLog = log;
            return found;
        }

        static bool TryResolveNavigationEdge(
            MovementTransition transition,
            Direction direction,
            AiNavigationState fromState,
            out Vector2Int neighborCell,
            out AiNavigationState neighborState,
            out MovementTransitionKind edgeKind) {
            neighborCell = default;
            neighborState = default;
            edgeKind = transition.Kind;

            switch (transition.Kind) {
                case MovementTransitionKind.Walkable:
                    neighborCell = fromState.Cell + direction.ToGridDelta();
                    neighborState = new AiNavigationState(
                        neighborCell,
                        transition.TargetLevel,
                        transition.TargetDice ?? fromState.StandingDice);
                    return true;

                case MovementTransitionKind.CanRoll:
                    if (!transition.HasDiceGridMovePlan) {
                        return false;
                    }

                    neighborCell = transition.DiceGridMovePlan.To.GridPos;
                    if (neighborCell == fromState.Cell) {
                        return false;
                    }

                    neighborState = new AiNavigationState(
                        neighborCell,
                        SurfaceHeightLevel.FromDiceStackTier(transition.DiceGridMovePlan.To.Tier),
                        fromState.StandingDice);
                    return true;

                default:
                    return false;
            }
        }

        static float ScoreNeighborCell(
            Vector2Int fromCell,
            Vector2Int neighborCell,
            Vector2Int goalCell,
            DiceController standOnDie,
            MovementTransitionKind edgeKind) {
            var currentDistance = DiceBoardAnalyzer.ManhattanDistance(fromCell, goalCell);
            var nextDistance = DiceBoardAnalyzer.ManhattanDistance(neighborCell, goalCell);
            var score = (currentDistance - nextDistance) * 10f;

            if (edgeKind == MovementTransitionKind.CanRoll) {
                score -= 1f;
            }

            if (standOnDie != null && neighborCell == standOnDie.CurrentState.GridPos) {
                score += 20f;
            }

            return score;
        }
    }
}
