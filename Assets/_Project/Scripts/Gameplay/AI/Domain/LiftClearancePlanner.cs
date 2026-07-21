using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct LiftClearancePlan
    {
        public DiceController BlockDie { get; }
        public Vector2Int PlaceCell { get; }
        public Direction FaceDirection { get; }

        public LiftClearancePlan(DiceController blockDie, Vector2Int placeCell, Direction faceDirection) {
            BlockDie = blockDie;
            PlaceCell = placeCell;
            FaceDirection = faceDirection;
        }
    }

    /// <summary>
    /// Plans lifting a nearby Top (or otherwise liftable) die aside so navigation can proceed.
    /// Requires the player to already be adjacent and able to lift.
    /// </summary>
    public static class LiftClearancePlanner
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static bool TryPlan(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            Vector2Int goalCell,
            out LiftClearancePlan plan) {
            plan = default;
            if (snapshot == null || registry == null || snapshot.PlayerIsCarrying) {
                return false;
            }

            var bestScore = float.MinValue;
            var found = false;
            LiftClearancePlan best = default;

            for (var i = 0; i < Directions.Length; i++) {
                var direction = Directions[i];
                var dieCell = snapshot.PlayerCell + direction.ToGridDelta();
                if (!snapshot.IsInPlayerRegion(dieCell)) {
                    continue;
                }

                if (!TryResolveLiftableDieAt(registry, dieCell, snapshot, out var blockDie)) {
                    continue;
                }

                if (!LiftPassability.CanLift(
                    snapshot.PlayerPlacement,
                    snapshot.PlayerIsOnFloor,
                    snapshot.StandingDice,
                    blockDie,
                    registry)) {
                    continue;
                }

                if (!TrySelectAsidePlaceCell(
                    snapshot,
                    registry,
                    blockDie,
                    goalCell,
                    out var placeCell,
                    out var placeScore)) {
                    continue;
                }

                var towardGoal = ScoreTowardGoal(snapshot.PlayerCell, dieCell, goalCell);
                var score = towardGoal * 10f + placeScore;
                if (score > bestScore) {
                    bestScore = score;
                    best = new LiftClearancePlan(blockDie, placeCell, direction);
                    found = true;
                }
            }

            if (!found) {
                return false;
            }

            plan = best;
            return true;
        }

        static bool TryResolveLiftableDieAt(
            DiceRegistry registry,
            Vector2Int cell,
            GameStateSnapshot snapshot,
            out DiceController die) {
            die = null;

            // Prefer Top blockers; floor/Bottom standing can lift Top or uncovered Bottom.
            if (registry.TryGetTopAt(cell, out var top) && top != null && top != snapshot.StandingDice) {
                die = top;
                return true;
            }

            if (snapshot.PlayerIsOnFloor
                && registry.TryGetBottomAt(cell, out var bottom)
                && bottom != null
                && bottom != snapshot.StandingDice
                && !registry.HasTopAt(cell)) {
                die = bottom;
                return true;
            }

            return false;
        }

        static bool TrySelectAsidePlaceCell(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            DiceController blockDie,
            Vector2Int goalCell,
            out Vector2Int placeCell,
            out float placeScore) {
            placeCell = default;
            placeScore = float.MinValue;
            var found = false;
            var blockCell = blockDie.CurrentState.GridPos;

            for (var i = 0; i < Directions.Length; i++) {
                var candidate = snapshot.PlayerCell + Directions[i].ToGridDelta();
                if (candidate == blockCell) {
                    continue;
                }

                if (!snapshot.IsInPlayerRegion(candidate)) {
                    continue;
                }

                if (!CarryPlacementPassability.TryResolveTarget(candidate, registry, out _, out _)) {
                    continue;
                }

                // Prefer not dropping onto the goal cell the player is trying to enter.
                var score = candidate == goalCell ? -5f : 0f;
                score -= DiceBoardAnalyzer.ManhattanDistance(candidate, goalCell) * 0.1f;

                if (score > placeScore) {
                    placeScore = score;
                    placeCell = candidate;
                    found = true;
                }
            }

            return found;
        }

        static float ScoreTowardGoal(Vector2Int playerCell, Vector2Int dieCell, Vector2Int goalCell) {
            var playerDist = DiceBoardAnalyzer.ManhattanDistance(playerCell, goalCell);
            var dieDist = DiceBoardAnalyzer.ManhattanDistance(dieCell, goalCell);
            return playerDist - dieDist;
        }
    }
}
