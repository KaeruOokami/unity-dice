using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class MatchGoalSelector
    {
        public static MatchGoal SelectBest(
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (snapshot == null || character == null || settings == null) {
                return null;
            }

            if (snapshot.PlayerIsCarrying) {
                return BuildCarryPlacementGoal(snapshot, registry, settings);
            }

            MatchGoal bestGoal = null;
            var bestScore = float.MinValue;

            for (var face = 2; face <= 6; face++) {
                var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.AllDice, face);
                for (var i = 0; i < clusters.Count; i++) {
                    var cluster = clusters[i];
                    var goal = BuildGoalForCluster(snapshot, face, cluster, registry, settings);
                    if (goal == null) {
                        continue;
                    }

                    if (goal.PriorityScore > bestScore) {
                        bestScore = goal.PriorityScore;
                        bestGoal = goal;
                    }
                }
            }

            return bestGoal;
        }

        static MatchGoal BuildCarryPlacementGoal(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var bestCell = Vector2Int.zero;
            var bestScore = float.MinValue;
            var found = false;

            for (var face = 2; face <= 6; face++) {
                var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.AllDice, face);
                for (var i = 0; i < clusters.Count; i++) {
                    var cluster = clusters[i];
                    if (cluster.Count >= face) {
                        continue;
                    }

                    foreach (var adjacent in GetClusterAdjacentCells(cluster)) {
                        if (!CarryPlacementPassability.TryResolveTarget(adjacent, registry, out _, out _)) {
                            continue;
                        }

                        var score = cluster.Count * settings.ClusterSizeWeight
                            - DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, adjacent);
                        if (score > bestScore) {
                            bestScore = score;
                            bestCell = adjacent;
                            found = true;
                        }
                    }
                }
            }

            if (!found) {
                foreach (var direction in new[] { Direction.East, Direction.West, Direction.North, Direction.South }) {
                    var cell = snapshot.PlayerCell + direction.ToGridDelta();
                    if (CarryPlacementPassability.TryResolveTarget(cell, registry, out _, out _)) {
                        bestCell = cell;
                        found = true;
                        break;
                    }
                }
            }

            if (!found) {
                return null;
            }

            var subGoals = new List<AiSubGoal> {
                AiSubGoal.PlaceCarriedDie(bestCell)
            };
            return new MatchGoal(0, new List<DiceSnapshot>(), null, subGoals, bestScore, false);
        }

        static MatchGoal BuildGoalForCluster(
            GameStateSnapshot snapshot,
            int face,
            List<DiceSnapshot> cluster,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (cluster.Count == 0) {
                return null;
            }

            var isImmediate = cluster.Count >= face;
            if (isImmediate) {
                return BuildImmediateMatchGoal(snapshot, face, cluster, settings);
            }

            if (!ClusterSelectionEvaluator.TrySelectNearestExternalDie(
                cluster,
                face,
                snapshot.AllDice,
                snapshot.PlayerCell,
                out var workDie)
                || workDie.Controller == null) {
                return null;
            }

            var subGoals = new List<AiSubGoal>();
            if (!IsStandingOnDice(snapshot, workDie.Controller)) {
                subGoals.Add(AiSubGoal.ReachWorkDie(workDie.Controller));
            }

            if (workDie.TopFace != face) {
                subGoals.Add(AiSubGoal.OrientDie(workDie.Controller, face));
            }

            if (registry != null
                && WorkDieSlidePlanner.TrySelectJoinTargetCell(
                    cluster,
                    workDie,
                    registry,
                    out var joinCell,
                    out var joinTier)) {
                subGoals.Add(AiSubGoal.JoinCluster(workDie.Controller, face, joinCell, joinTier));
            }

            if (subGoals.Count == 0) {
                return null;
            }

            var distanceToWorkDie = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, workDie.GridPos);
            var score = ClusterSelectionEvaluator.ScoreCluster(
                cluster,
                snapshot.PlayerCell,
                distanceToWorkDie,
                settings);

            return new MatchGoal(
                face,
                cluster,
                workDie.Controller,
                subGoals,
                score,
                false);
        }

        static MatchGoal BuildImmediateMatchGoal(
            GameStateSnapshot snapshot,
            int face,
            List<DiceSnapshot> cluster,
            AiPlayerSettings settings) {
            var participant = SelectClusterParticipant(cluster, snapshot);
            if (participant.Controller == null) {
                return null;
            }

            var subGoals = new List<AiSubGoal>();
            if (!IsStandingOnDice(snapshot, participant.Controller)) {
                subGoals.Add(AiSubGoal.ReachWorkDie(participant.Controller));
            }

            var distance = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, participant.GridPos);
            var score = ClusterSelectionEvaluator.ScoreCluster(
                cluster,
                snapshot.PlayerCell,
                distance,
                settings) + settings.ImmediateMatchBonus;

            return new MatchGoal(
                face,
                cluster,
                participant.Controller,
                subGoals,
                score,
                true);
        }

        static DiceSnapshot SelectClusterParticipant(
            List<DiceSnapshot> cluster,
            GameStateSnapshot snapshot) {
            for (var i = 0; i < cluster.Count; i++) {
                if (IsStandingOnDice(snapshot, cluster[i].Controller)) {
                    return cluster[i];
                }
            }

            DiceSnapshot best = default;
            var bestDistance = int.MaxValue;
            var found = false;

            for (var i = 0; i < cluster.Count; i++) {
                var candidate = cluster[i];
                var distance = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, candidate.GridPos);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = candidate;
                    found = true;
                }
            }

            return found ? best : cluster[0];
        }

        static HashSet<Vector2Int> GetClusterAdjacentCells(List<DiceSnapshot> cluster) {
            var occupied = new HashSet<Vector2Int>();
            for (var i = 0; i < cluster.Count; i++) {
                occupied.Add(cluster[i].GridPos);
            }

            var adjacent = new HashSet<Vector2Int>();
            for (var i = 0; i < cluster.Count; i++) {
                foreach (var cell in DiceBoardAnalyzer.GetAdjacentCells(cluster[i].GridPos)) {
                    if (!occupied.Contains(cell)) {
                        adjacent.Add(cell);
                    }
                }
            }

            return adjacent;
        }

        static bool IsStandingOnDice(GameStateSnapshot snapshot, DiceController dice) {
            return snapshot.StandingDice == dice;
        }
    }
}
