using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class SinkingChainEvaluator
    {
        public static bool HasSinkingDiceOnBoard(IReadOnlyList<DiceSnapshot> allDice) {
            if (allDice == null) {
                return false;
            }

            for (var i = 0; i < allDice.Count; i++) {
                if (IsSinkErasing(allDice[i])) {
                    return true;
                }
            }

            return false;
        }

        public static int CountSinkingDice(int face, IReadOnlyList<DiceSnapshot> allDice) {
            if (allDice == null) {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (IsSinkErasing(snapshot) && snapshot.TopFace == face) {
                    count++;
                }
            }

            return count;
        }

        public static List<DiceSnapshot> GetSinkingDice(int face, IReadOnlyList<DiceSnapshot> allDice) {
            var sinking = new List<DiceSnapshot>();
            if (allDice == null) {
                return sinking;
            }

            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (IsSinkErasing(snapshot) && snapshot.TopFace == face) {
                    sinking.Add(snapshot);
                }
            }

            return sinking;
        }

        public static bool IsChainPossible(int face, IReadOnlyList<DiceSnapshot> allDice) {
            return CountSinkingDice(face, allDice) >= face - 1;
        }

        public static bool HasAnyChainPossibleFace(IReadOnlyList<DiceSnapshot> allDice) {
            for (var face = 2; face <= 6; face++) {
                if (IsChainPossible(face, allDice)) {
                    return true;
                }
            }

            return false;
        }

        public static int GetMinDistanceToSinkingSameFace(
            DiceSnapshot candidate,
            int face,
            IReadOnlyList<DiceSnapshot> allDice) {
            if (allDice == null) {
                return int.MaxValue;
            }

            var best = int.MaxValue;
            for (var i = 0; i < allDice.Count; i++) {
                var sinking = allDice[i];
                if (!IsSinkErasing(sinking) || sinking.TopFace != face) {
                    continue;
                }

                var distance = DiceBoardAnalyzer.ManhattanDistance(candidate.GridPos, sinking.GridPos);
                if (distance < best) {
                    best = distance;
                }
            }

            return best;
        }

        public static bool IsMatchAdjacentToSinkingGroup(
            Vector2Int cell,
            DiceStackTier tier,
            int face,
            IReadOnlyList<DiceSnapshot> allDice) {
            if (allDice == null) {
                return false;
            }

            var candidateSlot = new DiceSlot(cell, tier);
            for (var i = 0; i < allDice.Count; i++) {
                var sinking = allDice[i];
                if (!IsSinkErasing(sinking) || sinking.TopFace != face) {
                    continue;
                }

                var from = new DiceSlot(sinking.GridPos, sinking.Tier);
                if (DiceStackAdjacency.IsAdjacentForMatch(from, candidateSlot)) {
                    return true;
                }
            }

            return false;
        }

        public static bool TrySelectChainJoinTargetCell(
            int face,
            IReadOnlyList<DiceSnapshot> allDice,
            DiceSnapshot workDie,
            DiceRegistry registry,
            VersusArenaLayout versusLayout,
            PlayerSlot playerSlot,
            out Vector2Int targetCell,
            out DiceStackTier targetTier) {
            targetCell = default;
            targetTier = default;
            if (allDice == null || registry == null || workDie.Controller == null) {
                return false;
            }

            if (!IsChainPossible(face, allDice)) {
                return false;
            }

            var sinkingCells = CollectSinkingCells(face, allDice);
            var sinkingDice = GetSinkingDice(face, allDice);
            var bestScore = float.MinValue;
            var found = false;

            foreach (var cell in CollectCandidateJoinCells(sinkingCells, allDice)) {
                if (!AiRegionFilter.IsInPlayerRegion(versusLayout, playerSlot, cell)) {
                    continue;
                }

                if (!CarryPlacementPassability.TryResolveTarget(cell, registry, out var tier, out _)) {
                    continue;
                }

                if (!IsMatchAdjacentToSinkingGroup(cell, tier, face, allDice)) {
                    continue;
                }

                if (!ClusterSelectionEvaluator.HasMovableExternalNeighbor(
                    cell,
                    tier,
                    sinkingDice,
                    allDice,
                    workDie.Controller)) {
                    continue;
                }

                if (workDie.GridPos == cell) {
                    targetCell = cell;
                    targetTier = tier;
                    return true;
                }

                var score = -DiceBoardAnalyzer.ManhattanDistance(workDie.GridPos, cell)
                    - DiceBoardAnalyzer.ManhattanDistance(cell, GetSinkingCentroid(face, allDice));
                if (score > bestScore) {
                    bestScore = score;
                    targetCell = cell;
                    targetTier = tier;
                    found = true;
                }
            }

            return found;
        }

        static HashSet<Vector2Int> CollectSinkingCells(int face, IReadOnlyList<DiceSnapshot> allDice) {
            var cells = new HashSet<Vector2Int>();
            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (IsSinkErasing(snapshot) && snapshot.TopFace == face) {
                    cells.Add(snapshot.GridPos);
                }
            }

            return cells;
        }

        static HashSet<Vector2Int> CollectCandidateJoinCells(
            HashSet<Vector2Int> sinkingCells,
            IReadOnlyList<DiceSnapshot> allDice) {
            var candidates = new HashSet<Vector2Int>();
            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (!IsSinkErasing(snapshot)) {
                    continue;
                }

                foreach (var cell in DiceBoardAnalyzer.GetAdjacentCells(snapshot.GridPos)) {
                    if (!sinkingCells.Contains(cell)) {
                        candidates.Add(cell);
                    }
                }
            }

            return candidates;
        }

        static Vector2Int GetSinkingCentroid(int face, IReadOnlyList<DiceSnapshot> allDice) {
            var sum = Vector2Int.zero;
            var count = 0;
            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (!IsSinkErasing(snapshot) || snapshot.TopFace != face) {
                    continue;
                }

                sum += snapshot.GridPos;
                count++;
            }

            if (count == 0) {
                return Vector2Int.zero;
            }

            return new Vector2Int(sum.x / count, sum.y / count);
        }

        static bool IsSinkErasing(DiceSnapshot snapshot) {
            return snapshot.Controller != null && snapshot.Controller.IsSinkErasing;
        }
    }
}
