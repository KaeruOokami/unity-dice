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

            MatchGoal bestGoal = null;
            var bestScore = float.MinValue;

            if (TrySelectBestChainGoal(snapshot, registry, settings, failureMemory, out var chainGoal)
                && chainGoal.PriorityScore > bestScore) {
                bestScore = chainGoal.PriorityScore;
                bestGoal = chainGoal;
            }

            if (TrySelectBestGoal(snapshot, registry, settings, failureMemory, out var clusterGoal)
                && clusterGoal.PriorityScore > bestScore) {
                bestGoal = clusterGoal;
            }

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
            MatchGoalFailureMemory failureMemory,
            out MatchGoal bestGoal) {
            if (TrySelectBestGoalPass(
                snapshot,
                registry,
                settings,
                abandonStrandedIslands: true,
                failureMemory,
                out bestGoal)) {
                return true;
            }

            return TrySelectBestGoalPass(
                snapshot,
                registry,
                settings,
                abandonStrandedIslands: false,
                failureMemory,
                out bestGoal);
        }

        static bool TrySelectBestGoalPass(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings,
            bool abandonStrandedIslands,
            MatchGoalFailureMemory failureMemory,
            out MatchGoal bestGoal) {
            bestGoal = null;
            var bestScore = float.MinValue;
            var now = Time.time;

            for (var face = 2; face <= 6; face++) {
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

            // Prefer Lift-Join when a same-face work die can be carried onto a join slot.
            if (LiftJoinPlanner.TrySelectSameFaceLiftJoin(
                snapshot,
                registry,
                sinkingDice,
                face,
                snapshot.PlanningDice,
                forChain: true,
                out var sameFaceWorkDie,
                out var sameFaceLiftPlan)) {
                return BuildLiftJoinGoal(
                    face,
                    sinkingDice,
                    sameFaceWorkDie.Controller,
                    sameFaceLiftPlan,
                    ScoreChainGoal(
                        sinkingDice,
                        face,
                        snapshot.PlanningDice,
                        DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, sameFaceWorkDie.GridPos),
                        settings));
            }

            if (TrySelectExternalWorkDie(
                sinkingDice,
                face,
                snapshot,
                settings,
                preferChain: true,
                registry,
                out var workDie)
                && registry != null
                && SinkingChainEvaluator.TrySelectChainJoinTargetCell(
                    face,
                    snapshot.PlanningDice,
                    workDie,
                    registry,
                    snapshot.VersusLayout,
                    snapshot.PlayerSlot,
                    out var joinCell,
                    out var joinTier)) {
                var subGoals = new List<AiSubGoal>();
                if (!IsStandingOnDice(snapshot, workDie.Controller)) {
                    subGoals.Add(AiSubGoal.ReachWorkDie(workDie.Controller));
                }

                if (workDie.TopFace != face) {
                    subGoals.Add(AiSubGoal.OrientDie(workDie.Controller, face));
                }

                subGoals.Add(AiSubGoal.JoinCluster(workDie.Controller, face, joinCell, joinTier));

                var distanceToWorkDie = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, workDie.GridPos);
                var score = ScoreChainGoal(
                    sinkingDice,
                    face,
                    snapshot.PlanningDice,
                    distanceToWorkDie,
                    settings);

                return new MatchGoal(
                    face,
                    sinkingDice,
                    workDie.Controller,
                    subGoals,
                    score,
                    false);
            }

            return null;
        }

        static float ScoreChainGoal(
            List<DiceSnapshot> sinkingDice,
            int face,
            IReadOnlyList<DiceSnapshot> allDice,
            int distanceToWorkDie,
            AiPlayerSettings settings) {
            return ClusterSelectionEvaluator.ScoreCluster(
                sinkingDice,
                face,
                allDice,
                playerCell: default,
                distanceToWorkDie,
                settings,
                isImmediateMatch: false,
                isChainGoal: true);
        }

        static MatchGoal BuildCarryPlacementGoal(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var bestCell = Vector2Int.zero;
            var bestScore = float.MinValue;
            var found = false;

            foreach (var direction in new[] { Direction.East, Direction.West, Direction.North, Direction.South }) {
                var cell = snapshot.PlayerCell + direction.ToGridDelta();
                if (!snapshot.IsInPlayerRegion(cell)) {
                    continue;
                }

                if (!CarryPlacementPassability.TryResolveTarget(cell, registry, out var placeTier, out _)) {
                    continue;
                }

                float score = -DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, cell);
                for (var face = 2; face <= 6; face++) {
                    var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.PlanningDice, face);
                    for (var i = 0; i < clusters.Count; i++) {
                        var cluster = clusters[i];
                        if (cluster.Count >= face) {
                            continue;
                        }

                        if (!IsClusterAdjacentCell(cluster, cell)) {
                            continue;
                        }

                        var clusterTier = cluster[0].Tier;
                        if (placeTier != clusterTier
                            && !CarryPlacementPassability.CanPlaceAt(cell, clusterTier, registry, out _)) {
                            continue;
                        }

                        var clusterScore = ClusterSelectionEvaluator.ScoreCluster(
                            cluster,
                            face,
                            snapshot.PlanningDice,
                            snapshot.PlayerCell,
                            0,
                            settings);
                        if (clusterScore > score) {
                            score = clusterScore;
                        }
                    }
                }

                if (score > bestScore) {
                    bestScore = score;
                    bestCell = cell;
                    found = true;
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

        static bool IsClusterAdjacentCell(IReadOnlyList<DiceSnapshot> cluster, Vector2Int cell) {
            for (var i = 0; i < cluster.Count; i++) {
                if (DiceBoardAnalyzer.ManhattanDistance(cluster[i].GridPos, cell) == 1) {
                    return true;
                }
            }

            return false;
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

            // Prefer Lift-Join when a same-face work die can be carried onto a join slot.
            if (LiftJoinPlanner.TrySelectSameFaceLiftJoin(
                snapshot,
                registry,
                cluster,
                face,
                snapshot.PlanningDice,
                forChain: false,
                out var sameFaceWorkDie,
                out var sameFaceLiftPlan)) {
                var liftDistance = DiceBoardAnalyzer.ManhattanDistance(
                    snapshot.PlayerCell,
                    sameFaceWorkDie.GridPos);
                var liftScore = ClusterSelectionEvaluator.ScoreCluster(
                    cluster,
                    face,
                    snapshot.PlanningDice,
                    snapshot.PlayerCell,
                    liftDistance,
                    settings);
                return BuildLiftJoinGoal(
                    face,
                    cluster,
                    sameFaceWorkDie.Controller,
                    sameFaceLiftPlan,
                    liftScore);
            }

            if (TrySelectExternalWorkDie(
                cluster,
                face,
                snapshot,
                settings,
                preferChain: false,
                registry,
                out var workDie)
                && registry != null
                && WorkDieSlidePlanner.TrySelectJoinTargetCell(
                    cluster,
                    workDie,
                    snapshot.PlanningDice,
                    registry,
                    snapshot.VersusLayout,
                    snapshot.PlayerSlot,
                    out var joinCell,
                    out var joinTier)) {
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

            return null;
        }

        static bool TrySelectExternalWorkDie(
            List<DiceSnapshot> cluster,
            int face,
            GameStateSnapshot snapshot,
            AiPlayerSettings settings,
            bool preferChain,
            DiceRegistry registry,
            out DiceSnapshot workDie) {
            // Same-face first (Lift already failed): slide-join without Orient.
            if (ClusterSelectionEvaluator.TrySelectNearestExternalDie(
                cluster,
                face,
                snapshot.PlanningDice,
                snapshot.PlayerCell,
                settings,
                preferChain,
                out workDie,
                registry,
                requireMatchingTopFace: true)
                && workDie.Controller != null) {
                return true;
            }

            return ClusterSelectionEvaluator.TrySelectNearestExternalDie(
                cluster,
                face,
                snapshot.PlanningDice,
                snapshot.PlayerCell,
                settings,
                preferChain,
                out workDie,
                registry,
                requireMatchingTopFace: false)
                && workDie.Controller != null;
        }

        static MatchGoal BuildLiftJoinGoal(
            int face,
            IReadOnlyList<DiceSnapshot> cluster,
            DiceController workDie,
            LiftJoinPlan liftPlan,
            float score) {
            var subGoals = new List<AiSubGoal> {
                AiSubGoal.LiftDie(liftPlan.WorkDie),
                AiSubGoal.PlaceCarriedDie(liftPlan.PlaceCell)
            };

            return new MatchGoal(face, cluster, workDie, subGoals, score, false);
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
                settings,
                isImmediateMatch: true,
                isChainGoal: false);

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
