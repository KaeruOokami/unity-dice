using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class DiceBoardAnalyzer
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static List<List<DiceSnapshot>> FindFaceClusters(
            IReadOnlyList<DiceSnapshot> allDice,
            int face) {
            var lookup = BuildLookup(allDice, face);
            var visited = new HashSet<(Vector2Int, DiceStackTier)>();
            var clusters = new List<List<DiceSnapshot>>();

            foreach (var pair in lookup) {
                if (visited.Contains(pair.Key)) {
                    continue;
                }

                var cluster = FloodFill(lookup, pair.Key, visited);
                if (cluster.Count > 0) {
                    clusters.Add(cluster);
                }
            }

            return clusters;
        }

        public static bool AreOrthogonallyAdjacent(DiceSnapshot a, DiceSnapshot b) {
            return DiceStackAdjacency.IsAdjacentForMatch(
                new DiceSlot(a.GridPos, a.Tier),
                new DiceSlot(b.GridPos, b.Tier));
        }

        public static int ManhattanDistance(Vector2Int a, Vector2Int b) {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        public static Direction? GetPrimaryDirectionToward(Vector2Int from, Vector2Int to) {
            var delta = to - from;
            if (delta == Vector2Int.zero) {
                return null;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) {
                return delta.x > 0 ? Direction.East : Direction.West;
            }

            return delta.y > 0 ? Direction.North : Direction.South;
        }

        public static bool TryGetRollDirectionForTopFace(
            DiceOrientation orientation,
            int targetFace,
            out Direction direction) {
            foreach (var candidate in Directions) {
                if (orientation.Roll(candidate).Top == targetFace) {
                    direction = candidate;
                    return true;
                }
            }

            direction = default;
            return false;
        }

        public static bool IsMovable(DiceSnapshot snapshot) {
            if (snapshot.IsBusy || snapshot.IsErasing) {
                return false;
            }

            var capabilities = DiceBehaviorResolver.GetCapabilities(snapshot.Kind);
            return capabilities.CanBeLiftedByPlayer || capabilities.CanGridRoll;
        }

        public static IEnumerable<Vector2Int> GetAdjacentCells(Vector2Int cell) {
            for (var i = 0; i < Directions.Length; i++) {
                yield return cell + Directions[i].ToGridDelta();
            }
        }

        static Dictionary<(Vector2Int, DiceStackTier), DiceSnapshot> BuildLookup(
            IReadOnlyList<DiceSnapshot> allDice,
            int face) {
            var lookup = new Dictionary<(Vector2Int, DiceStackTier), DiceSnapshot>();

            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (snapshot.IsErasing || snapshot.TopFace != face) {
                    continue;
                }

                lookup[(snapshot.GridPos, snapshot.Tier)] = snapshot;
            }

            return lookup;
        }

        static List<DiceSnapshot> FloodFill(
            Dictionary<(Vector2Int, DiceStackTier), DiceSnapshot> lookup,
            (Vector2Int, DiceStackTier) start,
            HashSet<(Vector2Int, DiceStackTier)> visited) {
            var cluster = new List<DiceSnapshot>();
            var queue = new Queue<(Vector2Int, DiceStackTier)>();
            queue.Enqueue(start);

            while (queue.Count > 0) {
                var key = queue.Dequeue();
                if (visited.Contains(key) || !lookup.TryGetValue(key, out var snapshot)) {
                    continue;
                }

                visited.Add(key);
                cluster.Add(snapshot);
                EnqueueAdjacentKeys(lookup, key, queue);
            }

            return cluster;
        }

        static void EnqueueAdjacentKeys(
            Dictionary<(Vector2Int, DiceStackTier), DiceSnapshot> lookup,
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
