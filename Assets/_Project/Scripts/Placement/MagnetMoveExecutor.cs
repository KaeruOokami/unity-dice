using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;

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
            DiceRegistry registry) {
            if (origin == null || registry == null) {
                return false;
            }

            if (!MovementTransitionEvaluator.TryGetDirectionBetween(plan.From.GridPos, plan.To.GridPos, out var direction)) {
                return false;
            }

            if (!IronAdjacencyBlock.IsPlayerMovable(origin, registry)) {
                return false;
            }

            var chain = MagnetChainResolver.Collect(origin, direction, registry);
            if (!TryBuildSlideExecutions(chain, plan, direction, registry, out var executions, out _)) {
                return false;
            }

            return ExecuteSlides(executions);
        }

        public static bool TryExecuteGroundRoll(
            DiceController origin,
            DiceGridMovePlan plan,
            DiceRegistry registry,
            GridMovePlanBuilder gridPlanBuilder,
            PassabilityContext context) {
            if (origin == null || registry == null || gridPlanBuilder == null) {
                return false;
            }

            if (!IronAdjacencyBlock.IsPlayerMovable(origin, registry)) {
                return false;
            }

            var chain = MagnetChainResolver.Collect(origin, plan.Direction, registry);
            if (!TryBuildRollExecutions(chain, plan, registry, gridPlanBuilder, context, out var executions, out _)) {
                return false;
            }

            return ExecuteRolls(executions);
        }

        static bool TryBuildSlideExecutions(
            IReadOnlyList<DiceController> chain,
            DiceSlidePlan originPlan,
            Direction direction,
            DiceRegistry registry,
            out List<SlideExecution> executions,
            out string rejectReason) {
            executions = new List<SlideExecution>(chain.Count);
            rejectReason = null;
            var delta = originPlan.To.GridPos - originPlan.From.GridPos;

            foreach (var dice in chain) {
                if (!IronAdjacencyBlock.IsPlayerMovable(dice, registry)) {
                    rejectReason = $"chain-member-not-movable dice={dice.name}";
                    return false;
                }

                if (dice.IsBusy || dice.IsDissolving || dice.IsVanishing) {
                    rejectReason = $"chain-member-busy dice={dice.name}";
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
            }

            return executions.Count > 0;
        }

        static bool TryBuildRollExecutions(
            IReadOnlyList<DiceController> chain,
            DiceGridMovePlan originPlan,
            DiceRegistry registry,
            GridMovePlanBuilder gridPlanBuilder,
            PassabilityContext context,
            out List<RollExecution> executions,
            out string rejectReason) {
            executions = new List<RollExecution>(chain.Count);
            rejectReason = null;

            foreach (var dice in chain) {
                if (!IronAdjacencyBlock.IsPlayerMovable(dice, registry)) {
                    rejectReason = $"chain-member-not-movable dice={dice.name}";
                    return false;
                }

                if (dice.IsBusy || dice.IsDissolving || dice.IsVanishing) {
                    rejectReason = $"chain-member-busy dice={dice.name}";
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
            }

            return executions.Count > 0;
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
