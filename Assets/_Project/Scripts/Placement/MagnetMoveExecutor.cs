using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class MagnetMoveExecutor
    {
        struct SlideExecution
        {
            public DiceController Dice;
            public DiceSlidePlan Plan;
        }

        struct RollExecution
        {
            public DiceController Dice;
            public DiceGridMovePlan Plan;
        }

        public static bool TryExecuteSlide(
            DiceController origin,
            DiceSlidePlan plan,
            DiceRegistry registry,
            PlayerMatchActionContext actionContext,
            PlayerSlot actionOwner) {
            if (origin == null || registry == null) {
                return false;
            }

            if (!MovementTransitionEvaluator.TryGetDirectionBetween(plan.From.GridPos, plan.To.GridPos, out var direction)) {
                return false;
            }

            if (!origin.IsPlayerMovable) {
                return false;
            }

            if (!TryBuildSlideExecutions(origin, plan, direction, registry, out var executions, out _)) {
                return false;
            }

            RegisterMovingDice(actionContext, executions, static e => e.Dice, actionOwner);
            return ExecuteSlides(executions);
        }

        public static bool TryExecuteGroundRoll(
            DiceController origin,
            DiceGridMovePlan plan,
            DiceRegistry registry,
            GridMovePlanBuilder gridPlanBuilder,
            PassabilityContext context,
            PlayerMatchActionContext actionContext) {
            if (origin == null || registry == null || gridPlanBuilder == null) {
                return false;
            }

            if (!origin.IsPlayerMovable) {
                return false;
            }

            if (!TryBuildRollExecutions(origin, plan, registry, gridPlanBuilder, context, out var executions, out _)) {
                return false;
            }

            if (!context.MovementOwner.HasValue) {
                Debug.LogError("MagnetMoveExecutor: ground roll requires PassabilityContext.MovementOwner.");
                return false;
            }

            RegisterMovingDice(actionContext, executions, static e => e.Dice, context.MovementOwner.Value);
            return ExecuteRolls(executions);
        }

        static void RegisterMovingDice<T>(
            PlayerMatchActionContext actionContext,
            IReadOnlyList<T> executions,
            System.Func<T, DiceController> getDice,
            PlayerSlot actionOwner) {
            if (actionContext == null || executions == null) {
                return;
            }

            var movingDice = new List<DiceController>(executions.Count);
            for (var i = 0; i < executions.Count; i++) {
                movingDice.Add(getDice(executions[i]));
            }

            actionContext.RegisterActionDice(movingDice, actionOwner);
        }

        static bool TryBuildSlideExecutions(
            DiceController origin,
            DiceSlidePlan originPlan,
            Direction direction,
            DiceRegistry registry,
            out List<SlideExecution> executions,
            out string rejectReason) {
            executions = new List<SlideExecution>();
            rejectReason = null;
            var delta = originPlan.To.GridPos - originPlan.From.GridPos;

            if (!TryAddSlideMember(origin, direction, delta, registry, executions, out rejectReason)) {
                return false;
            }

            if (origin.Capabilities.HasMagnetCoupling) {
                AppendArmSlideExecutions(origin, direction, delta, registry, executions);
            }

            return executions.Count > 0;
        }

        static void AppendArmSlideExecutions(
            DiceController origin,
            Direction moveDirection,
            Vector2Int delta,
            DiceRegistry registry,
            List<SlideExecution> executions) {
            var tier = origin.CurrentState.Tier;
            var arm = new List<DiceController>();

            foreach (var armDirection in MagnetChainResolver.GetPerpendicularDirections(moveDirection)) {
                MagnetChainResolver.CollectArm(origin, armDirection, tier, registry, arm);
                for (var i = 0; i < arm.Count; i++) {
                    if (!TryAddSlideMember(arm[i], moveDirection, delta, registry, executions, out _)) {
                        break;
                    }
                }
            }
        }

        static bool TryAddSlideMember(
            DiceController dice,
            Direction direction,
            Vector2Int delta,
            DiceRegistry registry,
            List<SlideExecution> executions,
            out string rejectReason) {
            rejectReason = null;

            if (!CanParticipateInChainMove(dice, registry)) {
                rejectReason = $"chain-member-not-movable dice={dice.name}";
                return false;
            }

            var fromState = dice.CurrentState;
            var expectedGridPos = fromState.GridPos + delta;

            if (!DiceSlidePassability.TryEvaluate(fromState, direction, registry, out var memberPlan, out var memberReject)) {
                rejectReason = memberReject ?? $"chain-slide-failed dice={dice.name}";
                return false;
            }

            // Tier may change per member (e.g. Top -> Bottom on empty floor); DiceSlidePassability is authoritative.
            if (memberPlan.To.GridPos != expectedGridPos) {
                rejectReason = $"chain-slide-target-mismatch dice={dice.name}";
                return false;
            }

            executions.Add(new SlideExecution {
                Dice = dice,
                Plan = memberPlan
            });
            return true;
        }

        static bool TryBuildRollExecutions(
            DiceController origin,
            DiceGridMovePlan originPlan,
            DiceRegistry registry,
            GridMovePlanBuilder gridPlanBuilder,
            PassabilityContext context,
            out List<RollExecution> executions,
            out string rejectReason) {
            executions = new List<RollExecution>();
            rejectReason = null;

            if (!TryAddRollMember(origin, originPlan, registry, gridPlanBuilder, context, executions, out rejectReason)) {
                return false;
            }

            if (origin.Capabilities.HasMagnetCoupling) {
                AppendArmRollExecutions(origin, originPlan, registry, gridPlanBuilder, context, executions);
            }

            return executions.Count > 0;
        }

        static void AppendArmRollExecutions(
            DiceController origin,
            DiceGridMovePlan originPlan,
            DiceRegistry registry,
            GridMovePlanBuilder gridPlanBuilder,
            PassabilityContext context,
            List<RollExecution> executions) {
            var tier = origin.CurrentState.Tier;
            var arm = new List<DiceController>();

            foreach (var armDirection in MagnetChainResolver.GetPerpendicularDirections(originPlan.Direction)) {
                MagnetChainResolver.CollectArm(origin, armDirection, tier, registry, arm);
                for (var i = 0; i < arm.Count; i++) {
                    if (!TryAddRollMember(arm[i], originPlan, registry, gridPlanBuilder, context, executions, out _)) {
                        break;
                    }
                }
            }
        }

        static bool TryAddRollMember(
            DiceController dice,
            DiceGridMovePlan originPlan,
            DiceRegistry registry,
            GridMovePlanBuilder gridPlanBuilder,
            PassabilityContext context,
            List<RollExecution> executions,
            out string rejectReason) {
            rejectReason = null;

            if (!CanParticipateInChainMove(dice, registry)) {
                rejectReason = $"chain-member-not-movable dice={dice.name}";
                return false;
            }

            if (!gridPlanBuilder.TryBuild(
                dice.CurrentState,
                originPlan.Direction,
                originPlan.Distance,
                context,
                out var memberPlan,
                out var memberReject)) {
                rejectReason = memberReject ?? $"chain-roll-failed dice={dice.name}";
                return false;
            }

            executions.Add(new RollExecution {
                Dice = dice,
                Plan = memberPlan
            });
            return true;
        }

        static bool CanParticipateInChainMove(DiceController dice, DiceRegistry registry) {
            return dice.IsPlayerMovable
                && !dice.IsBusy
                && !dice.IsErasing
                && !dice.IsVanishing;
        }

        static bool ExecuteSlides(IReadOnlyList<SlideExecution> executions) {
            for (var i = 0; i < executions.Count; i++) {
                if (!executions[i].Dice.TryExecuteSlidePlanInternal(executions[i].Plan)) {
                    return false;
                }
            }

            return true;
        }

        static bool ExecuteRolls(IReadOnlyList<RollExecution> executions) {
            for (var i = 0; i < executions.Count; i++) {
                if (!executions[i].Dice.TryExecuteGroundMovePlanInternal(executions[i].Plan)) {
                    return false;
                }
            }

            return true;
        }
    }
}
