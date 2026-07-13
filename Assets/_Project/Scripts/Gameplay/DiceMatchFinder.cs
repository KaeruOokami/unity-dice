using System.Collections.Generic;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public static class DiceMatchFinder
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static List<List<DiceController>> FindMatchingClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice) {
            var results = new List<List<DiceController>>();
            var consumed = new HashSet<DiceController>();

            for (var face = 2; face <= 6; face++) {
                var lookup = BuildLookup(allDice, face, consumed);
                var visited = new HashSet<(Vector2Int, DiceStackTier)>();

                foreach (var pair in lookup) {
                    if (visited.Contains(pair.Key)) {
                        continue;
                    }

                    var cluster = FloodFill(lookup, pair.Key, visited);
                    if (cluster.Count < face) {
                        continue;
                    }

                    if (!HasActionParticipant(cluster, actionDice)) {
                        continue;
                    }

                    results.Add(cluster);
                    foreach (var dice in cluster) {
                        consumed.Add(dice);
                    }
                }
            }

            return results;
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

        static Dictionary<(Vector2Int, DiceStackTier), DiceController> BuildLookup(
            IReadOnlyList<DiceController> allDice,
            int face,
            HashSet<DiceController> consumed) {
            var lookup = new Dictionary<(Vector2Int, DiceStackTier), DiceController>();

            foreach (var dice in allDice) {
                if (dice == null || consumed.Contains(dice) || !IsMatchEligible(dice)) {
                    continue;
                }

                if (dice.CurrentState.Orientation.Top != face) {
                    continue;
                }

                var key = (dice.CurrentState.GridPos, dice.CurrentState.Tier);
                lookup[key] = dice;
            }

            return lookup;
        }

        static bool IsMatchEligible(DiceController dice) {
            return !dice.IsSpawning && !dice.IsRolling;
        }

        static List<DiceController> FloodFill(
            Dictionary<(Vector2Int, DiceStackTier), DiceController> lookup,
            (Vector2Int, DiceStackTier) start,
            HashSet<(Vector2Int, DiceStackTier)> visited) {
            var cluster = new List<DiceController>();
            var queue = new Queue<(Vector2Int, DiceStackTier)>();
            queue.Enqueue(start);

            while (queue.Count > 0) {
                var key = queue.Dequeue();
                if (visited.Contains(key) || !lookup.TryGetValue(key, out var dice)) {
                    continue;
                }

                visited.Add(key);
                cluster.Add(dice);
                EnqueueAdjacentKeys(lookup, key, queue);
            }

            return cluster;
        }

        static void EnqueueAdjacentKeys(
            Dictionary<(Vector2Int, DiceStackTier), DiceController> lookup,
            (Vector2Int cell, DiceStackTier tier) key,
            Queue<(Vector2Int, DiceStackTier)> queue) {
            var from = new DiceSlot(key.cell, key.tier);

            foreach (var direction in Directions) {
                var neighbor = new DiceSlot(key.cell + direction.ToGridDelta(), key.tier);
                if (!DiceStackAdjacency.IsAdjacentForMatch(from, neighbor)) {
                    continue;
                }

                var neighborKey = (neighbor.Cell, neighbor.Tier);
                if (lookup.ContainsKey(neighborKey)) {
                    queue.Enqueue(neighborKey);
                }
            }
        }
    }
}
