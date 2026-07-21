using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct LiftJoinPlan
    {
        public DiceController WorkDie { get; }
        public Vector2Int StanceCell { get; }
        public Vector2Int PlaceCell { get; }
        public DiceStackTier PlaceTier { get; }

        public LiftJoinPlan(
            DiceController workDie,
            Vector2Int stanceCell,
            Vector2Int placeCell,
            DiceStackTier placeTier) {
            WorkDie = workDie;
            StanceCell = stanceCell;
            PlaceCell = placeCell;
            PlaceTier = placeTier;
        }
    }

    /// <summary>
    /// Plans same-face Lift → Place onto a cluster join slot (no roll / face change).
    /// Stance must be orthogonally adjacent to both the work die and the place cell.
    /// </summary>
    public static class LiftJoinPlanner
    {
        public static bool TryPlanForCluster(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            IReadOnlyList<DiceSnapshot> cluster,
            DiceSnapshot workDie,
            int face,
            IReadOnlyList<DiceSnapshot> allDice,
            out LiftJoinPlan plan) {
            plan = default;
            if (snapshot == null
                || registry == null
                || cluster == null
                || cluster.Count == 0
                || workDie.Controller == null
                || workDie.TopFace != face) {
                return false;
            }

            if (!WorkDieSlidePlanner.TrySelectJoinTargetCell(
                cluster,
                workDie,
                allDice,
                registry,
                snapshot.VersusLayout,
                snapshot.PlayerSlot,
                out var joinCell,
                out var joinTier)) {
                return false;
            }

            return TryPlanToJoinCell(snapshot, registry, workDie, joinCell, joinTier, out plan);
        }

        public static bool TryPlanForChain(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            int face,
            DiceSnapshot workDie,
            out LiftJoinPlan plan) {
            plan = default;
            if (snapshot == null
                || registry == null
                || workDie.Controller == null
                || workDie.TopFace != face) {
                return false;
            }

            if (!SinkingChainEvaluator.TrySelectChainJoinTargetCell(
                face,
                snapshot.PlanningDice,
                workDie,
                registry,
                snapshot.VersusLayout,
                snapshot.PlayerSlot,
                out var joinCell,
                out var joinTier)) {
                return false;
            }

            return TryPlanToJoinCell(snapshot, registry, workDie, joinCell, joinTier, out plan);
        }

        public static bool TryPlanToJoinCell(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            DiceSnapshot workDie,
            Vector2Int joinCell,
            DiceStackTier joinTier,
            out LiftJoinPlan plan) {
            plan = default;
            if (snapshot == null || registry == null || workDie.Controller == null) {
                return false;
            }

            if (workDie.GridPos == joinCell && workDie.Tier == joinTier) {
                return false;
            }

            if (!WorkDieSlidePlanner.IsJoinLandingAvailable(
                registry,
                workDie.Controller,
                joinCell,
                joinTier)) {
                return false;
            }

            if (!snapshot.IsInPlayerRegion(joinCell)) {
                return false;
            }

            if (!TrySelectStanceCell(
                snapshot,
                registry,
                workDie.Controller,
                workDie.GridPos,
                joinCell,
                out var stanceCell)) {
                return false;
            }

            plan = new LiftJoinPlan(workDie.Controller, stanceCell, joinCell, joinTier);
            return true;
        }

        public static bool TrySelectStanceCell(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            DiceController workDie,
            Vector2Int workCell,
            Vector2Int placeCell,
            out Vector2Int stanceCell) {
            stanceCell = default;
            if (snapshot == null || registry == null || workDie == null) {
                return false;
            }

            var bestDistance = int.MaxValue;
            var found = false;

            foreach (var candidate in DiceBoardAnalyzer.GetAdjacentCells(workCell)) {
                if (!snapshot.IsInPlayerRegion(candidate)) {
                    continue;
                }

                if (DiceBoardAnalyzer.ManhattanDistance(candidate, placeCell) != 1) {
                    continue;
                }

                if (!CanLiftFromStance(snapshot, registry, candidate, workDie)) {
                    continue;
                }

                var distance = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, candidate);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    stanceCell = candidate;
                    found = true;
                }
            }

            return found;
        }

        static bool CanLiftFromStance(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            Vector2Int stanceCell,
            DiceController workDie) {
            ResolveStancePlacement(
                snapshot,
                registry,
                stanceCell,
                workDie,
                out var placement,
                out var isOnFloor,
                out var standingDice);

            return LiftPassability.CanLift(placement, isOnFloor, standingDice, workDie, registry);
        }

        static void ResolveStancePlacement(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            Vector2Int stanceCell,
            DiceController workDie,
            out CharacterPlacement placement,
            out bool isOnFloor,
            out DiceController standingDice) {
            if (snapshot.PlayerCell == stanceCell) {
                placement = snapshot.PlayerPlacement;
                isOnFloor = snapshot.PlayerIsOnFloor;
                standingDice = snapshot.StandingDice;
                return;
            }

            if (registry.TryGetTopAt(stanceCell, out var top)
                && top != null
                && top != workDie) {
                placement = CharacterPlacement.OnDice(stanceCell, DiceStackTier.Top, top);
                isOnFloor = false;
                standingDice = top;
                return;
            }

            if (registry.TryGetBottomAt(stanceCell, out var bottom)
                && bottom != null
                && bottom != workDie
                && !registry.HasTopAt(stanceCell)) {
                placement = CharacterPlacement.OnDice(stanceCell, DiceStackTier.Bottom, bottom);
                isOnFloor = false;
                standingDice = bottom;
                return;
            }

            placement = CharacterPlacement.OnFloor(stanceCell);
            isOnFloor = true;
            standingDice = null;
        }
    }
}
