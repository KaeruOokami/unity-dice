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

        /// <summary>
        /// Face gather clusters using match occupancy/weight (jumbo footprint, tier slots).
        /// Incomplete clusters are included; progress/immediate use <see cref="AiFaceCluster.Weight"/>.
        /// </summary>
        public static List<AiFaceCluster> FindFaceClusters(
            IReadOnlyList<DiceSnapshot> allDice,
            int face) {
            var clusters = new List<AiFaceCluster>();
            if (allDice == null || face < 2 || face > 6) {
                return clusters;
            }

            var controllers = new List<DiceController>(allDice.Count);
            for (var i = 0; i < allDice.Count; i++) {
                var controller = allDice[i].Controller;
                if (controller != null) {
                    controllers.Add(controller);
                }
            }

            var matchClusters = DiceMatchFinder.FindGatherClusters(controllers, face);
            for (var i = 0; i < matchClusters.Count; i++) {
                var match = matchClusters[i];
                if (match.Members == null || match.Members.Count == 0) {
                    continue;
                }

                var members = new List<DiceSnapshot>(match.Members.Count);
                for (var m = 0; m < match.Members.Count; m++) {
                    var dice = match.Members[m];
                    if (dice == null) {
                        continue;
                    }

                    members.Add(new DiceSnapshot(dice));
                }

                if (members.Count == 0) {
                    continue;
                }

                clusters.Add(new AiFaceCluster(members, match.Weight, match.MatchTier));
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
    }
}
