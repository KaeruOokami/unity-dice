using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Stage 1 layout: one empty cell between work and partner.
    /// One roll toward the partner lands adjacent with matching top face.
    /// </summary>
    public static class MlCurriculumOneMoveSeeder
    {
        public readonly struct Layout
        {
            public readonly Vector2Int WorkCell;
            public readonly Vector2Int PartnerCell;
            public readonly DiceOrientation WorkOrientation;
            public readonly DiceOrientation PartnerOrientation;
            public readonly Direction SolutionDirection;
            public readonly DiceKind WorkKind;
            public readonly DiceKind PartnerKind;
            public readonly DiceStackTier Tier;

            public Layout(
                Vector2Int workCell,
                Vector2Int partnerCell,
                DiceOrientation workOrientation,
                DiceOrientation partnerOrientation,
                Direction solutionDirection,
                DiceKind workKind,
                DiceKind partnerKind,
                DiceStackTier tier) {
                WorkCell = workCell;
                PartnerCell = partnerCell;
                WorkOrientation = workOrientation;
                PartnerOrientation = partnerOrientation;
                SolutionDirection = solutionDirection;
                WorkKind = workKind;
                PartnerKind = partnerKind;
                Tier = tier;
            }
        }

        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static bool TryCreateLayout(
            Board board,
            MlCurriculumStage stage,
            System.Random random,
            out Layout layout) {
            layout = default;
            if (board == null || stage == null) {
                return false;
            }

            var rng = random ?? new System.Random();
            var targetFace = stage.TargetFace;
            var partnerOrientation = DiceOrientation.CreateWithTopFace(targetFace);
            if (partnerOrientation.Top != targetFace) {
                Debug.LogError(
                    $"MlCurriculumOneMoveSeeder: Failed to build partner orientation for face={targetFace}.");
                return false;
            }

            if (!TryPickRandomSolution(
                    targetFace,
                    rng,
                    out var workOrientation,
                    out var solutionDirection)) {
                Debug.LogError(
                    $"MlCurriculumOneMoveSeeder: No work orientation yields top={targetFace} after one roll.");
                return false;
            }

            var candidates = CollectAnchorCells(board, solutionDirection);
            if (candidates.Count == 0) {
                Debug.LogError("MlCurriculumOneMoveSeeder: Board too small for one-move gap layout.");
                return false;
            }

            var partnerCell = candidates[rng.Next(candidates.Count)];
            var delta = solutionDirection.ToGridDelta();
            var workCell = partnerCell - delta * 2;
            var midCell = partnerCell - delta;
            if (!board.IsInside(workCell) || !board.IsInside(midCell) || !board.IsInside(partnerCell)) {
                return false;
            }

            layout = new Layout(
                workCell,
                partnerCell,
                workOrientation,
                partnerOrientation,
                solutionDirection,
                stage.WorkKind,
                stage.PartnerKind,
                DiceStackTier.Bottom);
            return true;
        }

        static List<Vector2Int> CollectAnchorCells(Board board, Direction solutionDirection) {
            var list = new List<Vector2Int>();
            var delta = solutionDirection.ToGridDelta();
            for (var y = 0; y < board.Height; y++) {
                for (var x = 0; x < board.Width; x++) {
                    var partner = new Vector2Int(x, y);
                    if (!board.IsInside(partner)) {
                        continue;
                    }

                    var mid = partner - delta;
                    var work = partner - delta * 2;
                    if (!board.IsInside(mid) || !board.IsInside(work)) {
                        continue;
                    }

                    list.Add(partner);
                }
            }

            return list;
        }

        /// <summary>
        /// Finds an orientation whose top is not the target, but becomes the target after rolling
        /// in <paramref name="direction"/>.
        /// </summary>
        static bool TryPickRandomSolution(
            int targetFace,
            System.Random rng,
            out DiceOrientation orientation,
            out Direction direction) {
            orientation = default;
            direction = default;
            var solutions = new List<(DiceOrientation orientation, Direction direction)>();

            for (var d = 0; d < Directions.Length; d++) {
                var candidateDirection = Directions[d];
                if (!TryFindOrientationForDirection(targetFace, candidateDirection, out var found)) {
                    continue;
                }

                solutions.Add((found, candidateDirection));
            }

            if (solutions.Count == 0) {
                return false;
            }

            var pick = solutions[rng.Next(solutions.Count)];
            orientation = pick.orientation;
            direction = pick.direction;
            return true;
        }

        static bool TryFindOrientationForDirection(
            int targetFace,
            Direction direction,
            out DiceOrientation orientation) {
            orientation = default;
            var visited = new HashSet<(int, int, int)>();
            var queue = new Queue<DiceOrientation>();
            queue.Enqueue(DiceOrientation.Default);

            while (queue.Count > 0) {
                var current = queue.Dequeue();
                var key = (current.Top, current.North, current.East);
                if (!visited.Add(key)) {
                    continue;
                }

                if (current.Top != targetFace && current.Roll(direction).Top == targetFace) {
                    orientation = current;
                    return true;
                }

                for (var i = 0; i < Directions.Length; i++) {
                    queue.Enqueue(current.Roll(Directions[i]));
                }
            }

            return false;
        }
    }
}
