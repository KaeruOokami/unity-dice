using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
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
            out DiceSnapshot workDie) {
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
