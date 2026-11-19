using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Scores and builds goals that clear face-1 dice via OneVanish:
    /// act on a die showing 1 while match-adjacent to an erasing die.
    /// Priority rises with more 1s and higher board fill.
    /// </summary>
    public static class FaceOneClearEvaluator
    {
        public const int FaceOne = 1;

        public static bool TryBuildGoal(
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings,
            out MatchGoal goal) {
            goal = null;
            if (snapshot == null || character == null || registry == null || settings == null) {
                return false;
            }

            var erasing = CollectErasingDice(snapshot.PlanningDice);
            if (erasing.Count == 0) {
                return false;
            }

            var oneCount = CountFaceOnes(snapshot.PlanningDice);
            if (oneCount <= 0) {
                return false;
            }

            if (!TrySelectWorkDie(
                snapshot,
                character,
                registry,
                settings,
                erasing,
                out var workDie,
                out var pathLength)) {
                return false;
            }

            var fill = ComputeBoardFillRatio(registry.Board, snapshot, registry);
            var score = ScoreFaceOneClear(oneCount, fill, pathLength, settings);

            var subGoals = new List<AiSubGoal>();
            if (snapshot.StandingDice != workDie.Controller) {
                subGoals.Add(AiSubGoal.ReachWorkDie(workDie.Controller));
            }

            // JoinCluster with face 1: path orients and lands adjacent to erasing (action required).
            subGoals.Add(AiSubGoal.JoinCluster(
                workDie.Controller,
                FaceOne,
                workDie.GridPos,
                workDie.Tier));

            goal = new MatchGoal(
                FaceOne,
                erasing,
                workDie.Controller,
                subGoals,
                score,
                isImmediateMatch: false,
                isChainGoal: false,
                isFaceOneClearGoal: true);
            return true;
        }

        public static float ScoreFaceOneClear(
            int oneCount,
            float boardFillRatio,
            int distanceToWorkDie,
            AiPlayerSettings settings) {
            if (settings == null) {
                return float.MinValue;
            }

            var fill = Mathf.Clamp01(boardFillRatio);
            var ones = Mathf.Max(0, oneCount);
            return settings.FaceOneClearBaseBonus
                + ones * settings.FaceOneCountWeight
                + fill * settings.BoardFillWeight
                + ones * fill * settings.FaceOneFillSynergyWeight
                - distanceToWorkDie * settings.PlayerDistancePenalty;
        }

        public static int CountFaceOnes(IReadOnlyList<DiceSnapshot> dice) {
            if (dice == null) {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < dice.Count; i++) {
                var snapshot = dice[i];
                if (snapshot.Controller == null
                    || snapshot.IsErasing
                    || snapshot.IsCarried
                    || snapshot.Controller.IsVanishing) {
                    continue;
                }

                if (snapshot.TopFace == FaceOne) {
                    count++;
                }
            }

            return count;
        }

        public static List<DiceSnapshot> CollectErasingDice(IReadOnlyList<DiceSnapshot> dice) {
            var erasing = new List<DiceSnapshot>();
            if (dice == null) {
                return erasing;
            }

            for (var i = 0; i < dice.Count; i++) {
                var snapshot = dice[i];
                if (snapshot.Controller == null || !snapshot.IsErasing) {
                    continue;
                }

                if (snapshot.Controller.IsVanishing) {
                    continue;
                }

                erasing.Add(snapshot);
            }

            return erasing;
        }

        public static bool HasErasingDice(IReadOnlyList<DiceSnapshot> dice) {
            if (dice == null) {
                return false;
            }

            for (var i = 0; i < dice.Count; i++) {
                if (dice[i].Controller != null
                    && dice[i].IsErasing
                    && !dice[i].Controller.IsVanishing) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Occupied floor cells / passable floor cells in the player region (Bottom occupancy).
        /// </summary>
        public static float ComputeBoardFillRatio(
            Board board,
            GameStateSnapshot snapshot,
            DiceRegistry registry) {
            if (board == null || snapshot == null || registry == null) {
                return 0f;
            }

            var passable = 0;
            var occupied = 0;
            var width = board.Width;
            var height = board.Height;
            for (var x = 0; x < width; x++) {
                for (var z = 0; z < height; z++) {
                    var cell = new Vector2Int(x, z);
                    if (!snapshot.IsInPlayerRegion(cell)) {
                        continue;
                    }

                    if (board.GetCell(cell) != CellType.Floor) {
                        continue;
                    }

                    passable++;
                    if (registry.HasBottomAt(cell) || registry.HasPendingBottomAt(cell)) {
                        occupied++;
                    }
                }
            }

            if (passable <= 0) {
                return 0f;
            }

            return (float)occupied / passable;
        }

        public static bool IsClearComplete(DiceState state, IReadOnlyList<DiceSnapshot> erasingCluster) {
            if (erasingCluster == null || erasingCluster.Count == 0) {
                return false;
            }

            if (state.Orientation.Top != FaceOne) {
                return false;
            }

            var workSlot = new DiceSlot(state.GridPos, state.Tier);
            for (var i = 0; i < erasingCluster.Count; i++) {
                var controller = erasingCluster[i].Controller;
                if (controller == null || !controller.IsErasing || controller.IsVanishing) {
                    continue;
                }

                var member = controller.CurrentState;
                var memberSlot = new DiceSlot(member.GridPos, member.Tier);
                if (DiceStackAdjacency.IsAdjacentForMatch(workSlot, memberSlot)) {
                    return true;
                }
            }

            return false;
        }

        static bool TrySelectWorkDie(
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings,
            List<DiceSnapshot> erasing,
            out DiceSnapshot workDie,
            out int pathLength) {
            workDie = default;
            pathLength = int.MaxValue;

            // Prefer dice already showing 1.
            if (TrySelectWorkDiePass(
                snapshot,
                character,
                registry,
                settings,
                erasing,
                requireFaceOne: true,
                out workDie,
                out pathLength)) {
                return true;
            }

            return TrySelectWorkDiePass(
                snapshot,
                character,
                registry,
                settings,
                erasing,
                requireFaceOne: false,
                out workDie,
                out pathLength);
        }

        static bool TrySelectWorkDiePass(
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings,
            List<DiceSnapshot> erasing,
            bool requireFaceOne,
            out DiceSnapshot workDie,
            out int pathLength) {
            workDie = default;
            pathLength = int.MaxValue;
            if (erasing == null || erasing.Count == 0) {
                return false;
            }

            var constraints = AiNavigationConstraints.None;
            if (!AiWorkDieReachContext.TryCreate(character, settings, constraints, out var reachContext)) {
                return false;
            }

            var bestScore = float.MinValue;
            var found = false;
            var dice = snapshot.PlanningDice;
            for (var i = 0; i < dice.Count; i++) {
                var candidate = dice[i];
                if (candidate.Controller == null
                    || candidate.IsErasing
                    || candidate.IsCarried
                    || candidate.Controller.IsVanishing
                    || !DiceBoardAnalyzer.IsMovable(candidate)) {
                    continue;
                }

                if (requireFaceOne && candidate.TopFace != FaceOne) {
                    continue;
                }

                if (!requireFaceOne && candidate.TopFace == FaceOne) {
                    continue;
                }

                if (!ClusterSelectionEvaluator.IsStandableWorkDie(
                    candidate,
                    snapshot.PlanningDice,
                    registry)) {
                    continue;
                }

                if (!AiMountFeasibility.CanStandOnDie(
                    reachContext,
                    candidate.Controller,
                    snapshot.PlanningDice,
                    out var candidatePathLength)) {
                    continue;
                }

                var erasingDistance = MinDistanceToCluster(candidate.GridPos, erasing);
                var score = -candidatePathLength * settings.PlayerDistancePenalty
                    - erasingDistance * 10f;
                if (candidate.TopFace == FaceOne) {
                    score += 50f;
                }

                if (score > bestScore) {
                    bestScore = score;
                    workDie = candidate;
                    pathLength = candidatePathLength;
                    found = true;
                }
            }

            return found;
        }

        static int MinDistanceToCluster(Vector2Int cell, List<DiceSnapshot> cluster) {
            var best = int.MaxValue;
            for (var i = 0; i < cluster.Count; i++) {
                var distance = DiceBoardAnalyzer.ManhattanDistance(cell, cluster[i].GridPos);
                if (distance < best) {
                    best = distance;
                }
            }

            return best;
        }
    }
}
