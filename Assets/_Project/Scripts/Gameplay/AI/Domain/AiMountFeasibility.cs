using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Pre-checks whether the player can stand on a target die before committing to navigate/mount.
    /// Uses the same passability Evaluate path as movement planning (including height reach).
    /// </summary>
    public static class AiMountFeasibility
    {
        public static bool CanStandOnDie(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceController targetDie,
            int maxSearchSteps,
            bool allowJump,
            IReadOnlyList<DiceSnapshot> planningDice = null) {
            return TryMeasureStandOnDie(
                passability,
                start,
                footingWorldY,
                movementOwner,
                targetDie,
                maxSearchSteps,
                allowJump,
                AiNavigationConstraints.None,
                planningDice,
                out _);
        }

        public static bool CanStandOnDie(
            in AiWorkDieReachContext reach,
            DiceController targetDie,
            IReadOnlyList<DiceSnapshot> planningDice,
            out int pathLength) {
            return TryMeasureStandOnDie(
                reach.Passability,
                reach.Start,
                reach.FootingWorldY,
                reach.MovementOwner,
                targetDie,
                reach.MaxSearchSteps,
                reach.AllowJump,
                reach.Constraints,
                planningDice,
                out pathLength);
        }

        public static bool TryMeasureStandOnDie(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceController targetDie,
            int maxSearchSteps,
            bool allowJump,
            AiNavigationConstraints constraints,
            IReadOnlyList<DiceSnapshot> planningDice,
            out int pathLength) {
            pathLength = int.MaxValue;
            if (passability == null || targetDie == null) {
                return false;
            }

            // Sink (non-ghost) stays mountable / pathable. Ghost sink and other erasures do not.
            if (targetDie.IsVanishing
                || targetDie.IsErasureGhost
                || (targetDie.IsErasing && !targetDie.IsSinkErasing)) {
                return false;
            }

            if (start.StandingDice == targetDie) {
                pathLength = 0;
                return true;
            }

            // Floor, already on the die cell: mount-in-place (FloorRecovery / spawn).
            if (start.StandingDice == null
                && start.Level == SurfaceHeightLevel.Floor
                && start.Cell == targetDie.CurrentState.GridPos) {
                if (targetDie.AllowsUnconditionalMount
                    || ClusterSelectionEvaluator.IsStandableWorkDie(
                        new DiceSnapshot(targetDie),
                        planningDice)) {
                    pathLength = 0;
                    return true;
                }

                return false;
            }

            if (TryFindDirectMountStep(
                passability,
                start,
                footingWorldY,
                movementOwner,
                targetDie,
                allowJump)) {
                pathLength = 1;
                return true;
            }

            if (maxSearchSteps <= 0) {
                return false;
            }

            if (!AiCellPathfinder.TryFindFirstStep(
                passability,
                start,
                targetDie.CurrentState.GridPos,
                footingWorldY,
                movementOwner,
                maxSearchSteps,
                constraints,
                out var step,
                out _,
                targetDie)) {
                return false;
            }

            pathLength = step.PathLength;
            return true;
        }

        static bool TryFindDirectMountStep(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceController targetDie,
            bool allowJump) {
            var targetCell = targetDie.CurrentState.GridPos;
            if (!AiCellMoveEvaluator.TryGetDirectionBetweenCells(start.Cell, targetCell, out var direction)) {
                return false;
            }

            var ground = passability.Evaluate(
                start.Cell,
                start.Level,
                direction,
                start.StandingDice,
                PassabilityContext.ForGround(footingWorldY, movementOwner));
            if (TransitionMountsTarget(ground, targetDie)) {
                return true;
            }

            if (!allowJump) {
                return false;
            }

            var jump = passability.Evaluate(
                start.Cell,
                start.Level,
                direction,
                start.StandingDice,
                PassabilityContext.Jump(true, true, footingWorldY, movementOwner));
            return TransitionMountsTarget(jump, targetDie);
        }

        static bool TransitionMountsTarget(MovementTransition transition, DiceController targetDie) {
            return transition.Kind == MovementTransitionKind.Walkable
                && transition.TargetDice == targetDie;
        }
    }
}
