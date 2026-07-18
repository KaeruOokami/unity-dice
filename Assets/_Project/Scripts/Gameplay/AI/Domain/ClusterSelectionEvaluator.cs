using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class ClusterSelectionEvaluator
    {
        public static float ScoreCluster(
            List<DiceSnapshot> cluster,
            Vector2Int playerCell,
            int distanceToWorkDie,
            AiPlayerSettings settings) {
            if (cluster == null || cluster.Count == 0 || settings == null) {
                return float.MinValue;
            }

            var compactness = ComputeCompactnessRatio(cluster);
            var score = cluster.Count * settings.ClusterSizeWeight
                + compactness * settings.ClusterCompactnessWeight
                - distanceToWorkDie * settings.PlayerDistancePenalty;

            if (HasImmovableDice(cluster)) {
                score -= settings.ImmovableClusterPenalty;
            }

            return score;
        }

        public static float ScoreCluster(
            List<DiceSnapshot> cluster,
            int face,
            IReadOnlyList<DiceSnapshot> allDice,
            Vector2Int playerCell,
            int distanceToWorkDie,
            AiPlayerSettings settings) {
            var score = ScoreCluster(cluster, playerCell, distanceToWorkDie, settings);
            if (SinkingChainEvaluator.IsChainPossible(face, allDice)) {
                score += settings.SinkingChainBonus;
            }

            return score;
        }

        public static float ComputeCompactnessRatio(List<DiceSnapshot> cluster) {
            if (cluster == null || cluster.Count == 0) {
                return 0f;
            }

            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            for (var i = 0; i < cluster.Count; i++) {
                var pos = cluster[i].GridPos;
                if (pos.x < minX) {
                    minX = pos.x;
                }

                if (pos.x > maxX) {
                    maxX = pos.x;
                }

                if (pos.y < minY) {
                    minY = pos.y;
                }

                if (pos.y > maxY) {
                    maxY = pos.y;
                }
            }

            var width = maxX - minX + 1;
            var height = maxY - minY + 1;
            var area = width * height;
            if (area <= 0) {
                return 0f;
            }

            return (float)cluster.Count / area;
        }

        public static int GetDistanceToCluster(Vector2Int cell, List<DiceSnapshot> cluster) {
            var best = int.MaxValue;
            for (var i = 0; i < cluster.Count; i++) {
                var distance = DiceBoardAnalyzer.ManhattanDistance(cell, cluster[i].GridPos);
                if (distance < best) {
                    best = distance;
                }
            }

            return best;
        }

        public static bool TrySelectNearestExternalDie(
            List<DiceSnapshot> cluster,
            int clusterFace,
            IReadOnlyList<DiceSnapshot> allDice,
            Vector2Int playerCell,
            AiPlayerSettings settings,
            bool preferChain,
            out DiceSnapshot workDie,
            IDicePlacement placement = null) {
            workDie = default;
            var bestScore = float.MinValue;
            var found = false;
            var clusterCells = GetClusterCells(cluster);

            for (var i = 0; i < allDice.Count; i++) {
                var candidate = allDice[i];
                if (candidate.IsErasing || candidate.IsBusy || candidate.Controller == null) {
                    continue;
                }

                if (candidate.TopFace == clusterFace) {
                    continue;
                }

                if (ClusterContains(cluster, candidate)) {
                    continue;
                }

                if (clusterCells.Contains(candidate.GridPos)) {
                    continue;
                }

                if (!DiceBoardAnalyzer.IsMovable(candidate)) {
                    continue;
                }

                // Bottom under Top cannot be stood on for Reach/Orient.
                if (!IsStandableWorkDie(candidate, allDice, placement)) {
                    continue;
                }

                var distanceToCluster = GetDistanceToCluster(candidate.GridPos, cluster);
                var distanceToPlayer = DiceBoardAnalyzer.ManhattanDistance(playerCell, candidate.GridPos);
                var score = -distanceToCluster * 10f - distanceToPlayer;

                if (preferChain && settings != null) {
                    var sinkingDistance = SinkingChainEvaluator.GetMinDistanceToSinkingSameFace(
                        candidate,
                        clusterFace,
                        allDice);
                    if (sinkingDistance < int.MaxValue) {
                        score -= sinkingDistance * settings.SinkingChainWorkDieWeight;
                    }
                }

                if (score > bestScore) {
                    bestScore = score;
                    workDie = candidate;
                    found = true;
                }
            }

            return found;
        }

        public static bool IsStandableWorkDie(
            DiceSnapshot die,
            IReadOnlyList<DiceSnapshot> allDice,
            IDicePlacement placement = null) {
            if (die.Controller == null) {
                return false;
            }

            if (die.Tier != DiceStackTier.Bottom) {
                return true;
            }

            if (placement != null) {
                return !placement.HasTopAt(die.GridPos);
            }

            if (allDice == null) {
                return true;
            }

            for (var i = 0; i < allDice.Count; i++) {
                var other = allDice[i];
                if (other.Controller == null
                    || other.Controller == die.Controller
                    || other.IsErasing) {
                    continue;
                }

                if (other.GridPos == die.GridPos && other.Tier == DiceStackTier.Top) {
                    return false;
                }
            }

            return true;
        }

        public static bool ClusterContains(IReadOnlyList<DiceSnapshot> cluster, DiceSnapshot candidate) {
            for (var i = 0; i < cluster.Count; i++) {
                if (cluster[i].Controller == candidate.Controller) {
                    return true;
                }
            }

            return false;
        }

        public static bool ClusterContainsController(IReadOnlyList<DiceSnapshot> cluster, DiceController controller) {
            if (controller == null) {
                return false;
            }

            for (var i = 0; i < cluster.Count; i++) {
                if (cluster[i].Controller == controller) {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetStrandedIsolatedNonSinkingCluster(
            GameStateSnapshot snapshot,
            out int face,
            out List<DiceSnapshot> cluster) {
            face = 0;
            cluster = null;

            var standing = snapshot?.StandingDice;
            if (standing == null
                || standing.IsSinkErasing
                || standing.IsErasing
                || standing.IsVanishing
                || snapshot.PlanningDice == null) {
                return false;
            }

            face = standing.CurrentState.Orientation.Top;
            if (face < 2 || face > 6) {
                return false;
            }

            var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.PlanningDice, face);
            for (var i = 0; i < clusters.Count; i++) {
                var candidate = clusters[i];
                if (!ClusterContainsController(candidate, standing)) {
                    continue;
                }

                if (AiFloorRecoveryPlanner.HasAdjacentClusterExternalDie(
                    candidate,
                    face,
                    snapshot.PlanningDice)) {
                    return false;
                }

                cluster = candidate;
                return true;
            }

            return false;
        }

        public static bool IsStrandedIsolatedNonSinkingCluster(
            GameStateSnapshot snapshot,
            int face,
            IReadOnlyList<DiceSnapshot> cluster) {
            return TryGetStrandedIsolatedNonSinkingCluster(
                    snapshot,
                    out var strandedFace,
                    out var strandedCluster)
                && strandedFace == face
                && IsSameCluster(cluster, strandedCluster);
        }

        public static bool HasRetargetableCluster(
            GameStateSnapshot snapshot,
            int excludeFace,
            IReadOnlyList<DiceSnapshot> excludeCluster,
            AiPlayerSettings settings,
            IDicePlacement placement = null) {
            if (snapshot?.PlanningDice == null || settings == null) {
                return false;
            }

            for (var face = 2; face <= 6; face++) {
                var clusters = DiceBoardAnalyzer.FindFaceClusters(snapshot.PlanningDice, face);
                for (var i = 0; i < clusters.Count; i++) {
                    var cluster = clusters[i];
                    if (face == excludeFace && IsSameCluster(cluster, excludeCluster)) {
                        continue;
                    }

                    if (placement != null) {
                        if (cluster.Count >= face) {
                            if (!ShouldDiscardImmediateCluster(cluster, placement)) {
                                return true;
                            }

                            continue;
                        }

                        if (ShouldDiscardIncompleteCluster(cluster, face, snapshot, placement)) {
                            continue;
                        }
                    }
                    else if (cluster.Count >= face) {
                        return true;
                    }

                    if (TrySelectNearestExternalDie(
                        cluster,
                        face,
                        snapshot.PlanningDice,
                        snapshot.PlayerCell,
                        settings,
                        preferChain: false,
                        out _,
                        placement)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsStandingOnCluster(
            GameStateSnapshot snapshot,
            IReadOnlyList<DiceSnapshot> cluster) {
            return snapshot?.StandingDice != null
                && ClusterContainsController(cluster, snapshot.StandingDice);
        }

        public static bool IsSameCluster(
            IReadOnlyList<DiceSnapshot> left,
            IReadOnlyList<DiceSnapshot> right) {
            if (left == null || right == null || left.Count == 0 || right.Count == 0) {
                return false;
            }

            if (left.Count != right.Count) {
                return false;
            }

            for (var i = 0; i < left.Count; i++) {
                if (!ClusterContains(right, left[i])) {
                    return false;
                }
            }

            return true;
        }

        public static HashSet<Vector2Int> GetClusterCells(IReadOnlyList<DiceSnapshot> cluster) {
            var cells = new HashSet<Vector2Int>();
            if (cluster == null) {
                return cells;
            }

            for (var i = 0; i < cluster.Count; i++) {
                cells.Add(cluster[i].GridPos);
            }

            return cells;
        }

        public static bool ShouldDiscardImmediateCluster(
            IReadOnlyList<DiceSnapshot> cluster,
            IDicePlacement placement) {
            if (cluster == null || cluster.Count == 0 || placement == null) {
                return true;
            }

            // Top-tier members are standable; Bottom under full Tops is not.
            if (GetClusterTier(cluster) == DiceStackTier.Top) {
                return false;
            }

            for (var i = 0; i < cluster.Count; i++) {
                if (!placement.HasTopAt(cluster[i].GridPos)) {
                    return false;
                }
            }

            return true;
        }

        public static bool ShouldDiscardIncompleteCluster(
            IReadOnlyList<DiceSnapshot> cluster,
            int face,
            GameStateSnapshot snapshot,
            IDicePlacement placement) {
            if (cluster == null || cluster.Count == 0 || placement == null || face < 2) {
                return true;
            }

            var need = face - cluster.Count;
            if (need <= 0) {
                return false;
            }

            return CountSameTierExpansionCapacity(cluster, snapshot, placement) < need;
        }

        public static int CountSameTierExpansionCapacity(
            IReadOnlyList<DiceSnapshot> cluster,
            GameStateSnapshot snapshot,
            IDicePlacement placement) {
            if (cluster == null || cluster.Count == 0 || placement == null) {
                return 0;
            }

            var tier = GetClusterTier(cluster);
            var clusterCells = GetClusterCells(cluster);
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();

            foreach (var cell in clusterCells) {
                foreach (var adjacent in DiceBoardAnalyzer.GetAdjacentCells(cell)) {
                    if (clusterCells.Contains(adjacent) || visited.Contains(adjacent)) {
                        continue;
                    }

                    if (!IsSameTierPlaceable(adjacent, tier, snapshot, placement)) {
                        continue;
                    }

                    visited.Add(adjacent);
                    queue.Enqueue(adjacent);
                }
            }

            while (queue.Count > 0) {
                var current = queue.Dequeue();
                foreach (var adjacent in DiceBoardAnalyzer.GetAdjacentCells(current)) {
                    if (clusterCells.Contains(adjacent) || visited.Contains(adjacent)) {
                        continue;
                    }

                    if (!IsSameTierPlaceable(adjacent, tier, snapshot, placement)) {
                        continue;
                    }

                    visited.Add(adjacent);
                    queue.Enqueue(adjacent);
                }
            }

            return visited.Count;
        }

        static DiceStackTier GetClusterTier(IReadOnlyList<DiceSnapshot> cluster) {
            return cluster[0].Tier;
        }

        static bool IsSameTierPlaceable(
            Vector2Int cell,
            DiceStackTier tier,
            GameStateSnapshot snapshot,
            IDicePlacement placement) {
            if (snapshot != null && !snapshot.IsInPlayerRegion(cell)) {
                return false;
            }

            return CarryPlacementPassability.CanPlaceAt(cell, tier, placement, out _);
        }

        public static bool HasMovableExternalNeighbor(
            Vector2Int cell,
            DiceStackTier tier,
            IReadOnlyList<DiceSnapshot> cluster,
            IReadOnlyList<DiceSnapshot> allDice,
            DiceController excludeDie = null) {
            if (allDice == null) {
                return false;
            }

            var targetSlot = new DiceSlot(cell, tier);
            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (!DiceBoardAnalyzer.IsMovable(snapshot)) {
                    continue;
                }

                if (ClusterContains(cluster, snapshot)) {
                    continue;
                }

                if (excludeDie != null && snapshot.Controller == excludeDie) {
                    continue;
                }

                var dieSlot = new DiceSlot(snapshot.GridPos, snapshot.Tier);
                if (DiceStackAdjacency.IsAdjacentForLift(targetSlot, dieSlot)) {
                    return true;
                }
            }

            return false;
        }

        static bool HasImmovableDice(List<DiceSnapshot> cluster) {
            for (var i = 0; i < cluster.Count; i++) {
                if (!DiceBoardAnalyzer.IsMovable(cluster[i])) {
                    return true;
                }
            }

            return false;
        }
    }
}
