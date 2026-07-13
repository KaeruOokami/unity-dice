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

            var hasSinking = SinkingChainEvaluator.HasSinkingDiceOnBoard(snapshot.PlanningDice);
            var chainPriority = hasSinking && SinkingChainEvaluator.HasAnyChainPossibleFace(snapshot.PlanningDice);
            if (chainPriority
                && TrySelectBestChainGoal(snapshot, registry, settings, out var chainGoal)) {
                return chainGoal;
            }

            TrySelectBestGoal(snapshot, registry, settings, chainPriority, out var bestGoal);
            return bestGoal;
        }

        static bool TrySelectBestChainGoal(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings,
            out MatchGoal bestGoal) {
            bestGoal = null;
            var bestScore = float.MinValue;

            for (var face = 2; face <= 6; face++) {
                if (!SinkingChainEvaluator.IsChainPossible(face, snapshot.PlanningDice)) {
                    continue;
                }

                var goal = BuildGoalForChain(snapshot, face, registry, settings);
                if (goal == null) {
                    continue;
                }

                if (goal.PriorityScore > bestScore) {
                    bestScore = goal.PriorityScore;
                    bestGoal = goal;
                }
            }

            return bestGoal != null;
        }

        static bool TrySelectBestGoal(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings,
            bool suppressChainFaces,
            out MatchGoal bestGoal) {
            bestGoal = null;
            var bestScore = float.MinValue;

            for (var face = 2; face <= 6; face++) {
                if (suppressChainFaces
                    && SinkingChainEvaluator.IsChainPossible(face, snapshot.PlanningDice)) {
                    continue;
                }

                var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.PlanningDice, face);
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

            return bestGoal != null;
        }

        static MatchGoal BuildGoalForChain(
            GameStateSnapshot snapshot,
            int face,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (!SinkingChainEvaluator.IsChainPossible(face, snapshot.PlanningDice)) {
                return null;
            }

            var sinkingDice = SinkingChainEvaluator.GetSinkingDice(face, snapshot.PlanningDice);
            if (sinkingDice.Count == 0) {
                return null;
            }

            if (!ClusterSelectionEvaluator.TrySelectNearestExternalDie(
                sinkingDice,
                face,
                snapshot.PlanningDice,
                snapshot.PlayerCell,
                settings,
                preferChain: true,
                out var workDie)
                || workDie.Controller == null) {
                return null;
            }

            if (registry == null
                || !SinkingChainEvaluator.TrySelectChainJoinTargetCell(
                    face,
                    snapshot.PlanningDice,
                    workDie,
                    registry,
                    snapshot.VersusLayout,
                    snapshot.PlayerSlot,
                    out var joinCell,
                    out var joinTier)) {
                return null;
            }

            var subGoals = new List<AiSubGoal>();
            if (!IsStandingOnDice(snapshot, workDie.Controller)) {
                subGoals.Add(AiSubGoal.ReachWorkDie(workDie.Controller));
            }

            if (workDie.TopFace != face) {
                subGoals.Add(AiSubGoal.OrientDie(workDie.Controller, face));
            }

            subGoals.Add(AiSubGoal.JoinCluster(workDie.Controller, face, joinCell, joinTier));

            var distanceToWorkDie = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, workDie.GridPos);
            var score = ScoreChainGoal(sinkingDice.Count, distanceToWorkDie, settings);

            return new MatchGoal(
                face,
                sinkingDice,
                workDie.Controller,
                subGoals,
                score,
                false);
        }

        static float ScoreChainGoal(
            int sinkingCount,
            int distanceToWorkDie,
            AiPlayerSettings settings) {
            return sinkingCount * settings.ClusterSizeWeight
                + settings.SinkingChainBonus
                - distanceToWorkDie * settings.PlayerDistancePenalty;
        }

        static MatchGoal BuildCarryPlacementGoal(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var bestCell = Vector2Int.zero;
            var bestScore = float.MinValue;
            var found = false;

            for (var face = 2; face <= 6; face++) {
                var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.PlanningDice, face);
                for (var i = 0; i < clusters.Count; i++) {
                    var cluster = clusters[i];
                    if (cluster.Count >= face) {
                        continue;
                    }

                    foreach (var adjacent in GetClusterAdjacentCells(cluster)) {
                        if (!snapshot.IsInPlayerRegion(adjacent)) {
                            continue;
                        }

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
                    if (!snapshot.IsInPlayerRegion(cell)) {
                        continue;
                    }

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
                snapshot.PlanningDice,
                snapshot.PlayerCell,
                settings,
                preferChain: false,
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
                    snapshot.PlanningDice,
                    registry,
                    snapshot.VersusLayout,
                    snapshot.PlayerSlot,
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
                face,
                snapshot.PlanningDice,
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
                face,
                snapshot.PlanningDice,
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
