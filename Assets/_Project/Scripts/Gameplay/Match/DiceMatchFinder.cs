using System.Collections.Generic;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public readonly struct DiceMatchCluster
    {
        public List<DiceController> Members { get; }
        public int Weight { get; }
        public DiceStackTier MatchTier { get; }

        public DiceMatchCluster(List<DiceController> members, int weight, DiceStackTier matchTier) {
            Members = members;
            Weight = weight;
            MatchTier = matchTier;
        }
    }

    public static class DiceMatchFinder
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        static readonly DiceStackTier[] BothTiers = {
            DiceStackTier.Bottom,
            DiceStackTier.Top
        };

        static readonly List<Vector2Int> FootprintBuffer = new(JumboFootprint.CellCount);

        public static List<DiceMatchCluster> FindMatchingClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice) {
            var results = new List<DiceMatchCluster>();
            // Pre-sink jumbo is consumed as a whole dice; sinking matches are per (dice, tier).
            var consumedDice = new HashSet<DiceController>();
            var consumedSlots = new HashSet<(DiceController, DiceStackTier)>();

            for (var face = 2; face <= 6; face++) {
                FindPreSinkBridgedClusters(allDice, actionDice, face, consumedDice, consumedSlots, results);
                FindSinkingTierClusters(
                    allDice, actionDice, face, DiceStackTier.Bottom, consumedDice, consumedSlots, results);
                FindSinkingTierClusters(
                    allDice, actionDice, face, DiceStackTier.Top, consumedDice, consumedSlots, results);
            }

            return results;
        }

        /// <summary>
        /// Pre-sink jumbo: weight 1, bridges Bottom 2x2 and Top 2x2 into one cluster.
        /// Normal Bottom/Top still do not connect except through that jumbo.
        /// </summary>
        static void FindPreSinkBridgedClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice,
            int face,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots,
            List<DiceMatchCluster> results) {
            var lookup = BuildPreSinkBridgedLookup(allDice, face, consumedDice, consumedSlots);
            var visited = new HashSet<(Vector2Int, DiceStackTier)>();

            foreach (var pair in lookup) {
                if (visited.Contains(pair.Key)) {
                    continue;
                }

                var cluster = FloodFillBridged(lookup, pair.Key, visited);
                if (cluster.Weight < face || !HasActionParticipant(cluster.Members, actionDice)) {
                    continue;
                }

                // Bridged pass is only meaningful when a pre-sink jumbo is involved.
                if (!ContainsPreSinkJumbo(cluster.Members)) {
                    continue;
                }

                results.Add(cluster);
                foreach (var dice in cluster.Members) {
                    consumedDice.Add(dice);
                    consumedSlots.Add((dice, DiceStackTier.Bottom));
                    consumedSlots.Add((dice, DiceStackTier.Top));
                }
            }
        }

        /// <summary>
        /// Sinking (and normal same-tier) matches: no Bottom/Top bridge.
        /// Sinking jumbo contributes weight 4 on the active tier only.
        /// </summary>
        static void FindSinkingTierClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice,
            int face,
            DiceStackTier matchTier,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots,
            List<DiceMatchCluster> results) {
            var lookup = BuildSameTierLookup(allDice, face, matchTier, consumedDice, consumedSlots);
            var visited = new HashSet<Vector2Int>();

            foreach (var pair in lookup) {
                if (visited.Contains(pair.Key)) {
                    continue;
                }

                var cluster = FloodFillSameTier(lookup, pair.Key, visited, matchTier);
                if (cluster.Weight < face || !HasActionParticipant(cluster.Members, actionDice)) {
                    continue;
                }

                results.Add(cluster);
                foreach (var dice in cluster.Members) {
                    consumedSlots.Add((dice, matchTier));
                    if (dice.Capabilities.HasExpandedFootprint && dice.IsSinkErasing) {
                        // Sinking jumbo may still match the other tier later.
                        continue;
                    }

                    if (!dice.Capabilities.HasExpandedFootprint) {
                        consumedDice.Add(dice);
                    }
                }
            }
        }

        static bool ContainsPreSinkJumbo(IReadOnlyList<DiceController> members) {
            for (var i = 0; i < members.Count; i++) {
                var dice = members[i];
                if (dice != null
                    && dice.Capabilities.HasExpandedFootprint
                    && !dice.IsSinkErasing) {
                    return true;
                }
            }

            return false;
        }

        static bool HasActionParticipant(
            IReadOnlyList<DiceController> cluster,
            IReadOnlyCollection<DiceController> actionDice) {
            if (actionDice == null || actionDice.Count == 0) {
                return false;
            }

            foreach (var dice in cluster) {
                foreach (var participant in actionDice) {
                    if (dice == participant) {
                        return true;
                    }
                }
            }

            return false;
        }

        static Dictionary<(Vector2Int, DiceStackTier), DiceController> BuildPreSinkBridgedLookup(
            IReadOnlyList<DiceController> allDice,
            int face,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots) {
            var lookup = new Dictionary<(Vector2Int, DiceStackTier), DiceController>();

            foreach (var dice in allDice) {
                if (dice == null
                    || consumedDice.Contains(dice)
                    || !IsMatchEligible(dice)
                    || dice.CurrentState.Orientation.Top != face) {
                    continue;
                }

                // Sinking jumbos use the per-tier pass.
                if (dice.Capabilities.HasExpandedFootprint && dice.IsSinkErasing) {
                    continue;
                }

                if (dice.Capabilities.HasExpandedFootprint) {
                    FootprintBuffer.Clear();
                    JumboFootprint.AppendCells(dice.CurrentState.GridPos, FootprintBuffer);
                    for (var i = 0; i < FootprintBuffer.Count; i++) {
                        var cell = FootprintBuffer[i];
                        lookup[(cell, DiceStackTier.Bottom)] = dice;
                        lookup[(cell, DiceStackTier.Top)] = dice;
                    }

                    continue;
                }

                var tier = dice.CurrentState.Tier;
                if (consumedSlots.Contains((dice, tier))) {
                    continue;
                }

                lookup[(dice.CurrentState.GridPos, tier)] = dice;
            }

            return lookup;
        }

        static Dictionary<Vector2Int, DiceController> BuildSameTierLookup(
            IReadOnlyList<DiceController> allDice,
            int face,
            DiceStackTier matchTier,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots) {
            var lookup = new Dictionary<Vector2Int, DiceController>();

            foreach (var dice in allDice) {
                if (dice == null
                    || !IsMatchEligible(dice)
                    || dice.CurrentState.Orientation.Top != face) {
                    continue;
                }

                if (consumedSlots.Contains((dice, matchTier))) {
                    continue;
                }

                // Pre-sink jumbo already handled (or skipped) in bridged pass.
                if (dice.Capabilities.HasExpandedFootprint && !dice.IsSinkErasing) {
                    continue;
                }

                if (dice.Capabilities.HasExpandedFootprint) {
                    var weight = DiceMatchWeight.Get(dice, matchTier);
                    if (weight <= 0) {
                        continue;
                    }

                    FootprintBuffer.Clear();
                    JumboFootprint.AppendCells(dice.CurrentState.GridPos, FootprintBuffer);
                    for (var i = 0; i < FootprintBuffer.Count; i++) {
                        lookup[FootprintBuffer[i]] = dice;
                    }

                    continue;
                }

                if (consumedDice.Contains(dice) || dice.CurrentState.Tier != matchTier) {
                    continue;
                }

                lookup[dice.CurrentState.GridPos] = dice;
            }

            return lookup;
        }

        static bool IsMatchEligible(DiceController dice) {
            return !dice.IsSpawning && !dice.IsRolling;
        }

        static DiceMatchCluster FloodFillBridged(
            Dictionary<(Vector2Int, DiceStackTier), DiceController> lookup,
            (Vector2Int, DiceStackTier) start,
            HashSet<(Vector2Int, DiceStackTier)> visited) {
            var members = new List<DiceController>();
            var memberSet = new HashSet<DiceController>();
            var queue = new Queue<(Vector2Int, DiceStackTier)>();
            queue.Enqueue(start);
            var weight = 0;

            while (queue.Count > 0) {
                var key = queue.Dequeue();
                if (visited.Contains(key) || !lookup.TryGetValue(key, out var dice)) {
                    continue;
                }

                visited.Add(key);
                if (memberSet.Add(dice)) {
                    members.Add(dice);
                    weight += DiceMatchWeight.GetPreSinkBridgedWeight(dice);
                }

                EnqueueBridgedNeighbors(lookup, key, dice, queue);
            }

            return new DiceMatchCluster(members, weight, DiceStackTier.Bottom);
        }

        static void EnqueueBridgedNeighbors(
            Dictionary<(Vector2Int, DiceStackTier), DiceController> lookup,
            (Vector2Int cell, DiceStackTier tier) key,
            DiceController dice,
            Queue<(Vector2Int, DiceStackTier)> queue) {
            // Same-tier orthogonal (normal adjacency).
            foreach (var direction in Directions) {
                var neighbor = (key.cell + direction.ToGridDelta(), key.tier);
                if (lookup.ContainsKey(neighbor)) {
                    queue.Enqueue(neighbor);
                }
            }

            // Pre-sink jumbo bridges Bottom and Top across its footprint only.
            if (dice == null
                || !dice.Capabilities.HasExpandedFootprint
                || dice.IsSinkErasing) {
                return;
            }

            FootprintBuffer.Clear();
            JumboFootprint.AppendCells(dice.CurrentState.GridPos, FootprintBuffer);
            for (var i = 0; i < FootprintBuffer.Count; i++) {
                var cell = FootprintBuffer[i];
                for (var t = 0; t < BothTiers.Length; t++) {
                    var footprintKey = (cell, BothTiers[t]);
                    if (lookup.ContainsKey(footprintKey)) {
                        queue.Enqueue(footprintKey);
                    }
                }
            }
        }

        static DiceMatchCluster FloodFillSameTier(
            Dictionary<Vector2Int, DiceController> lookup,
            Vector2Int start,
            HashSet<Vector2Int> visited,
            DiceStackTier matchTier) {
            var members = new List<DiceController>();
            var memberSet = new HashSet<DiceController>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            var weight = 0;

            while (queue.Count > 0) {
                var cell = queue.Dequeue();
                if (visited.Contains(cell) || !lookup.TryGetValue(cell, out var dice)) {
                    continue;
                }

                visited.Add(cell);
                if (memberSet.Add(dice)) {
                    members.Add(dice);
                    weight += DiceMatchWeight.Get(dice, matchTier);
                }

                foreach (var direction in Directions) {
                    var neighbor = cell + direction.ToGridDelta();
                    if (lookup.ContainsKey(neighbor)) {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return new DiceMatchCluster(members, weight, matchTier);
        }
    }
}
