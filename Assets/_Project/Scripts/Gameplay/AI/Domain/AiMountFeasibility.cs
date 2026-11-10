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
                return true;
            }

            // Floor, already on the die cell: mount-in-place (FloorRecovery / spawn).
            if (start.StandingDice == null
                && start.Level == SurfaceHeightLevel.Floor
                && start.Cell == targetDie.CurrentState.GridPos) {
                return targetDie.AllowsUnconditionalMount
                    || ClusterSelectionEvaluator.IsStandableWorkDie(
                        new DiceSnapshot(targetDie),
                        planningDice);
            }

            if (TryFindDirectMountStep(
                passability,
                start,
                footingWorldY,
                movementOwner,
                targetDie,
                allowJump)) {
                return true;
            }

            if (maxSearchSteps <= 0) {
                return false;
            }

            return AiCellPathfinder.TryFindFirstStep(
                passability,
                start,
                targetDie.CurrentState.GridPos,
                footingWorldY,
                movementOwner,
                maxSearchSteps,
                AiNavigationConstraints.None,
                out _,
                out _,
                targetDie);
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
