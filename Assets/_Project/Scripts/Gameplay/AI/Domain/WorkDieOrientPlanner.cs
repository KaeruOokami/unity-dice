using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class WorkDieOrientPlanner
    {
        public static bool TryBuildOrientPlan(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            int targetFace,
            bool allowJump,
            out WorkDieSlidePlan plan) {
            plan = default;
            if (passability == null || workDie == null) {
                return false;
            }

            if (!WorkDieRollPathPlanner.TryFindOrientPath(
                passability,
                workDie,
                fromLevel,
                footingWorldY,
                movementOwner,
                startState,
                targetFace,
                allowJump,
                out var directions)
                || directions == null
                || directions.Count == 0) {
                return false;
            }

            plan = new WorkDieSlidePlan(startState.GridPos, startState.Orientation, directions);
            return true;
        }

        public static bool TrySelectNextStep(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            WorkDieSlidePlan plan,
            int stepIndex,
            bool allowJump,
            out WorkDieRollStep step,
            out int remainingRolls) {
            step = default;
            remainingRolls = 0;
            if (!WorkDieSlidePlanner.TrySelectNextStep(
                passability,
                workDie,
                fromLevel,
                footingWorldY,
                movementOwner,
                plan,
                stepIndex,
                allowJump,
                out step)) {
                return false;
            }

            remainingRolls = plan.Directions.Count - stepIndex - 1;
            return true;
        }
    }
}
