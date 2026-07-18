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

        static readonly List<Vector2Int> FootprintBuffer = new(JumboFootprint.CellCount);

        public static List<DiceMatchCluster> FindMatchingClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice) {
            var results = new List<DiceMatchCluster>();
            // Consumed per (dice, tier) so a sinking jumbo can match on Bottom and Top separately.
            var consumed = new HashSet<(DiceController, DiceStackTier)>();

            for (var face = 2; face <= 6; face++) {
                TryFindForFace(allDice, actionDice, face, DiceStackTier.Bottom, consumed, results);
                TryFindForFace(allDice, actionDice, face, DiceStackTier.Top, consumed, results);
            }

            return results;
        }

        static void TryFindForFace(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice,
            int face,
            DiceStackTier matchTier,
            HashSet<(DiceController, DiceStackTier)> consumed,
            List<DiceMatchCluster> results) {
            var lookup = BuildLookup(allDice, face, matchTier, consumed);
            var visited = new HashSet<Vector2Int>();

            foreach (var pair in lookup) {
                if (visited.Contains(pair.Key)) {
                    continue;
                }

                var cluster = FloodFill(lookup, pair.Key, visited, matchTier);
                if (cluster.Weight < face) {
                    continue;
                }

                if (!HasActionParticipant(cluster.Members, actionDice)) {
                    continue;
                }

                results.Add(cluster);
                foreach (var dice in cluster.Members) {
                    consumed.Add((dice, matchTier));
                }
            }
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

        static Dictionary<Vector2Int, DiceController> BuildLookup(
            IReadOnlyList<DiceController> allDice,
            int face,
            DiceStackTier matchTier,
            HashSet<(DiceController, DiceStackTier)> consumed) {
            var lookup = new Dictionary<Vector2Int, DiceController>();

            foreach (var dice in allDice) {
                if (dice == null
                    || consumed.Contains((dice, matchTier))
                    || !IsMatchEligible(dice)) {
                    continue;
                }

                if (dice.CurrentState.Orientation.Top != face) {
                    continue;
                }

                var weight = DiceMatchWeight.Get(dice, matchTier);
                if (weight <= 0) {
                    continue;
                }

                if (dice.Capabilities.HasExpandedFootprint) {
                    FootprintBuffer.Clear();
                    JumboFootprint.AppendCells(dice.CurrentState.GridPos, FootprintBuffer);
                    for (var i = 0; i < FootprintBuffer.Count; i++) {
                        lookup[FootprintBuffer[i]] = dice;
                    }
                } else if (dice.CurrentState.Tier == matchTier) {
                    lookup[dice.CurrentState.GridPos] = dice;
                }
            }

            return lookup;
        }

        static bool IsMatchEligible(DiceController dice) {
            return !dice.IsSpawning && !dice.IsRolling;
        }

        static DiceMatchCluster FloodFill(
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

                EnqueueAdjacentCells(lookup, cell, queue);
            }

            return new DiceMatchCluster(members, weight, matchTier);
        }

        static void EnqueueAdjacentCells(
            Dictionary<Vector2Int, DiceController> lookup,
            Vector2Int cell,
            Queue<Vector2Int> queue) {
            foreach (var direction in Directions) {
                var neighbor = cell + direction.ToGridDelta();
                if (lookup.ContainsKey(neighbor)) {
                    queue.Enqueue(neighbor);
                }
            }
        }
    }
}
