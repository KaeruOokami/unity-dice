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

        public static List<List<DiceController>> FindMatchingClusters(IReadOnlyList<DiceController> allDice) {
            var results = new List<List<DiceController>>();
            var consumed = new HashSet<DiceController>();

            for (var face = 2; face <= 6; face++) {
                var lookup = BuildLookup(allDice, face, consumed);
                var visited = new HashSet<Vector2Int>();

                foreach (var pair in lookup) {
                    if (visited.Contains(pair.Key)) {
                        continue;
                    }

                    var cluster = FloodFill(lookup, pair.Key, visited);
                    if (cluster.Count < face) {
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

        static Dictionary<Vector2Int, DiceController> BuildLookup(
            IReadOnlyList<DiceController> allDice,
            int face,
            HashSet<DiceController> consumed) {
            var lookup = new Dictionary<Vector2Int, DiceController>();

            foreach (var dice in allDice) {
                if (dice == null || consumed.Contains(dice)) {
                    continue;
                }

                if (dice.CurrentState.Orientation.Top != face) {
                    continue;
                }

                lookup[dice.CurrentState.GridPos] = dice;
            }

            return lookup;
        }

        static List<DiceController> FloodFill(
            Dictionary<Vector2Int, DiceController> lookup,
            Vector2Int start,
            HashSet<Vector2Int> visited) {
            var cluster = new List<DiceController>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0) {
                var pos = queue.Dequeue();
                if (visited.Contains(pos) || !lookup.ContainsKey(pos)) {
                    continue;
                }

                visited.Add(pos);
                cluster.Add(lookup[pos]);

                foreach (var direction in Directions) {
                    queue.Enqueue(pos + direction.ToGridDelta());
                }
            }

            return cluster;
        }
    }
}
