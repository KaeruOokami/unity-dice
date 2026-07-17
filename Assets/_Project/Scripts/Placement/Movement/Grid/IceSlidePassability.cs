using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public static class IceSlidePassability
    {
        public static bool TryBuildUntilBlocked(
            DiceState fromState,
            Direction direction,
            IDicePlacement placement,
            out DiceSlidePlan plan,
            out string rejectReason) {
            return TryBuildUntilBlocked(
                fromState,
                direction,
                placement,
                out plan,
                out _,
                out rejectReason);
        }

        public static bool TryBuildUntilBlocked(
            DiceState fromState,
            Direction direction,
            IDicePlacement placement,
            out DiceSlidePlan plan,
            out DiceController elasticTransferTarget,
            out string rejectReason) {
            plan = default;
            elasticTransferTarget = null;
            rejectReason = null;

            if (placement is not DiceRegistry registry) {
                rejectReason = "ghost-swap-requires-dice-registry";
                return false;
            }

            var current = fromState;
            var steps = 0;
            string stepRejectReason = null;
            DiceSlidePlan lastStep = default;
            var exitByBlockedStep = false;

            while (true) {
                if (!DiceSlidePassability.TryEvaluate(
                    current,
                    direction,
                    registry,
                    out var stepPlan,
                    out stepRejectReason)) {
                    exitByBlockedStep = true;
                    break;
                }

                steps++;
                lastStep = stepPlan;

                if (stepPlan.HasGhostSwap) {
                    current = stepPlan.To;
                    break;
                }

                // If Ice falls to a lower tier, stop there.
                // (Top -> Bottom is considered a "fall" to a lower level.)
                if (current.Tier == DiceStackTier.Top
                    && stepPlan.To.Tier == DiceStackTier.Bottom) {
                    current = stepPlan.To;
                    break;
                }

                current = stepPlan.To;
            }

            if (exitByBlockedStep) {
                TryResolveElasticTransferTarget(
                    current,
                    direction,
                    registry,
                    out elasticTransferTarget);
            }

            if (steps == 0) {
                if (elasticTransferTarget != null) {
                    plan = new DiceSlidePlan(fromState, fromState);
                    return true;
                }

                rejectReason = stepRejectReason ?? "no-slide-step";
                return false;
            }

            plan = lastStep.HasGhostSwap
                ? DiceSlidePlan.WithRetargetedFrom(lastStep, fromState)
                : new DiceSlidePlan(fromState, current);
            return true;
        }

        public static bool TryResolveElasticTransferTarget(
            DiceState stoppedState,
            Direction direction,
            DiceRegistry registry,
            out DiceController transferTarget) {
            transferTarget = null;
            if (registry == null) {
                return false;
            }

            var nextPos = stoppedState.GridPos + direction.ToGridDelta();
            if (!registry.TryGetDiceAt(nextPos, stoppedState.Tier, out var candidate)
                || candidate == null
                || candidate.IsBusy
                || !candidate.Capabilities.TransfersSlideOnCollision) {
                return false;
            }

            transferTarget = candidate;
            return true;
        }

        public static bool HasSlideDisplacement(DiceSlidePlan plan) {
            return plan.HasGhostSwap
                || plan.From.GridPos != plan.To.GridPos
                || plan.From.Tier != plan.To.Tier;
        }

        /// <summary>
        /// When a slide path crosses the versus partition, returns the last cell on the
        /// start side (where a riding player should dismount).
        /// </summary>
        public static bool TryGetPartitionDismountCell(
            DiceSlidePlan fullPlan,
            Direction direction,
            VersusArenaLayout layout,
            out Vector2Int dismountCell) {
            dismountCell = fullPlan.From.GridPos;

            if (layout == null
                || fullPlan.HasGhostSwap
                || !HasSlideDisplacement(fullPlan)
                || !layout.CrossesPartition(fullPlan.From.GridPos, fullPlan.To.GridPos)) {
                return false;
            }

            var cell = fullPlan.From.GridPos;
            var end = fullPlan.To.GridPos;
            var guard = 0;
            while (cell != end && guard++ < 64) {
                var next = cell + direction.ToGridDelta();
                if (layout.CrossesPartition(cell, next)) {
                    dismountCell = cell;
                    return true;
                }

                cell = next;
            }

            return false;
        }
    }
}
