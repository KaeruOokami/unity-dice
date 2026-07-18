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
            AiPlayerSettings settings,
            MatchGoalFailureMemory failureMemory = null) {
            if (snapshot == null || character == null || settings == null) {
                return null;
            }

            if (snapshot.PlayerIsCarrying) {
                return BuildCarryPlacementGoal(snapshot, registry, settings);
            }

            var hasSinking = SinkingChainEvaluator.HasSinkingDiceOnBoard(snapshot.PlanningDice);
            var chainPriority = hasSinking && SinkingChainEvaluator.HasAnyChainPossibleFace(snapshot.PlanningDice);
            if (chainPriority
                && TrySelectBestChainGoal(snapshot, registry, settings, failureMemory, out var chainGoal)) {
                return chainGoal;
            }

            TrySelectBestGoal(snapshot, registry, settings, chainPriority, failureMemory, out var bestGoal);
            return bestGoal;
        }

        static bool TrySelectBestChainGoal(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings,
            MatchGoalFailureMemory failureMemory,
            out MatchGoal bestGoal) {
            bestGoal = null;
            var bestScore = float.MinValue;
            var now = Time.time;

            for (var face = 2; face <= 6; face++) {
                if (!SinkingChainEvaluator.IsChainPossible(face, snapshot.PlanningDice)) {
                    continue;
                }

                var goal = BuildGoalForChain(snapshot, face, registry, settings);
                if (goal == null || IsExcluded(goal, failureMemory, now)) {
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
            MatchGoalFailureMemory failureMemory,
            out MatchGoal bestGoal) {
            if (TrySelectBestGoalPass(
                snapshot,
                registry,
                settings,
                suppressChainFaces,
                abandonStrandedIslands: true,
                failureMemory,
                out bestGoal)) {
                return true;
            }

            return TrySelectBestGoalPass(
                snapshot,
                registry,
                settings,
                suppressChainFaces,
                abandonStrandedIslands: false,
                failureMemory,
                out bestGoal);
        }

        static bool TrySelectBestGoalPass(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings,
            bool suppressChainFaces,
            bool abandonStrandedIslands,
            MatchGoalFailureMemory failureMemory,
            out MatchGoal bestGoal) {
            bestGoal = null;
            var bestScore = float.MinValue;
            var now = Time.time;

            for (var face = 2; face <= 6; face++) {
                if (suppressChainFaces
                    && SinkingChainEvaluator.IsChainPossible(face, snapshot.PlanningDice)) {
                    continue;
                }

                var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.PlanningDice, face);
                for (var i = 0; i < clusters.Count; i++) {
                    var cluster = clusters[i];
                    var goal = BuildGoalForCluster(
                        snapshot,
                        face,
                        cluster,
                        registry,
                        settings,
                        abandonStrandedIslands);
                    if (goal == null || IsExcluded(goal, failureMemory, now)) {
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

        static bool IsExcluded(MatchGoal goal, MatchGoalFailureMemory failureMemory, float nowSeconds) {
            return failureMemory != null && failureMemory.IsExcluded(goal, nowSeconds);
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
                out var workDie,
                registry)
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
            AiPlayerSettings settings,
            bool abandonStrandedIslands) {
            if (cluster.Count == 0) {
                return null;
            }

            var isImmediate = cluster.Count >= face;
            if (isImmediate) {
                return BuildImmediateMatchGoal(snapshot, face, cluster, registry, settings);
            }

            if (registry != null
                && ClusterSelectionEvaluator.ShouldDiscardIncompleteCluster(
                    cluster,
                    face,
                    snapshot,
                    registry)) {
                return null;
            }

            // Incomplete isolated non-sinking island: prefer other clusters so CanRoll can leave.
            if (abandonStrandedIslands
                && ClusterSelectionEvaluator.IsStrandedIsolatedNonSinkingCluster(snapshot, face, cluster)) {
                return null;
            }

            if (!ClusterSelectionEvaluator.TrySelectNearestExternalDie(
                cluster,
                face,
                snapshot.PlanningDice,
                snapshot.PlayerCell,
                settings,
                preferChain: false,
                out var workDie,
                registry)
                || workDie.Controller == null) {
                return null;
            }

            if (registry == null
                || !WorkDieSlidePlanner.TrySelectJoinTargetCell(
                    cluster,
                    workDie,
                    snapshot.PlanningDice,
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
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (registry != null
                && ClusterSelectionEvaluator.ShouldDiscardImmediateCluster(cluster, registry)) {
                return null;
            }

            var participant = SelectClusterParticipant(cluster, snapshot, registry);
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
            GameStateSnapshot snapshot,
            DiceRegistry registry) {
            for (var i = 0; i < cluster.Count; i++) {
                if (IsStandingOnDice(snapshot, cluster[i].Controller)
                    && ClusterSelectionEvaluator.IsStandableWorkDie(
                        cluster[i],
                        snapshot.PlanningDice,
                        registry)) {
                    return cluster[i];
                }
            }

            var standingLevel = snapshot.StandingDice != null
                ? SurfaceHeightLevel.FromDiceStackTier(snapshot.StandingDice.CurrentState.Tier)
                : SurfaceHeightLevel.Floor;

            // Prefer participants reachable without climbing (Bottom while on Bottom/Floor).
            if (TrySelectNearestParticipantAtOrBelowLevel(
                cluster,
                snapshot,
                standingLevel,
                registry,
                out var reachable)) {
                return reachable;
            }

            return SelectNearestParticipant(cluster, snapshot, registry);
        }

        static bool TrySelectNearestParticipantAtOrBelowLevel(
            List<DiceSnapshot> cluster,
            GameStateSnapshot snapshot,
            int maxLevel,
            DiceRegistry registry,
            out DiceSnapshot best) {
            best = default;
            var bestDistance = int.MaxValue;
            var found = false;

            for (var i = 0; i < cluster.Count; i++) {
                var candidate = cluster[i];
                if (candidate.Controller == null) {
                    continue;
                }

                if (!ClusterSelectionEvaluator.IsStandableWorkDie(
                    candidate,
                    snapshot.PlanningDice,
                    registry)) {
                    continue;
                }

                var candidateLevel = SurfaceHeightLevel.FromDiceStackTier(candidate.Tier);
                if (candidateLevel > maxLevel) {
                    continue;
                }

                var distance = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, candidate.GridPos);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = candidate;
                    found = true;
                }
            }

            return found;
        }

        static DiceSnapshot SelectNearestParticipant(
            List<DiceSnapshot> cluster,
            GameStateSnapshot snapshot,
            DiceRegistry registry) {
            DiceSnapshot best = default;
            var bestDistance = int.MaxValue;
            var found = false;

            for (var i = 0; i < cluster.Count; i++) {
                var candidate = cluster[i];
                if (!ClusterSelectionEvaluator.IsStandableWorkDie(
                    candidate,
                    snapshot.PlanningDice,
                    registry)) {
                    continue;
                }

                var distance = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, candidate.GridPos);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = candidate;
                    found = true;
                }
            }

            return found ? best : default;
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
