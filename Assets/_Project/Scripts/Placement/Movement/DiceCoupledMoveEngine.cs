using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// L2 dice-coupled movement engine. Switch on
    /// <see cref="DiceStandingMoveMode"/> then run slide/roll sub-engines.
    /// </summary>
    public sealed class DiceCoupledMoveEngine
    {
        readonly DiceRegistry registry;
        readonly GridMovePlanBuilder gridPlanBuilder;

        public DiceCoupledMoveEngine(DiceRegistry registry, GridMovePlanBuilder gridPlanBuilder) {
            this.registry = registry;
            this.gridPlanBuilder = gridPlanBuilder;
        }

        public bool TryEvaluateOnEmptyCell(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            out MovementTransition transition) {
            return TryEvaluate(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                direction,
                context,
                allowTierLanding: false,
                reach: default,
                out transition);
        }

        public bool TryEvaluateOnOccupiedCell(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            return TryEvaluate(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                direction,
                context,
                allowTierLanding: true,
                reach,
                out transition);
        }

        public bool TryEvaluateGridRoll(
            Vector2Int fromCell,
            Vector2Int toCell,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            int distance,
            bool allowMultiCell,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (standingDice == null || !fromSurface.AllowsRoll) {
                rejectReason = "no-standing-dice-or-surface-cannot-roll";
                return false;
            }

            var standingTier = SurfaceHeightLevel.ToDiceStackTier(fromSurface.Level);
            if (standingTier != standingDice.CurrentState.Tier) {
                rejectReason =
                    $"standing-tier-mismatch standingTier={standingTier} diceTier={standingDice.CurrentState.Tier}";
                return false;
            }

            if (!standingDice.Capabilities.CanGridRoll) {
                rejectReason = "dice-cannot-grid-roll";
                return false;
            }

            if (!standingDice.IsPlayerMovable) {
                rejectReason = "dice-not-player-movable";
                return false;
            }

            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance) {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (distance > 1 && !allowMultiCell) {
                rejectReason = "multi-cell-not-allowed";
                return false;
            }

            if (fromCell + direction.ToGridDelta() * distance != toCell) {
                rejectReason =
                    $"cell-mismatch from=({fromCell.x},{fromCell.y}) to=({toCell.x},{toCell.y}) " +
                    $"dir={direction} distance={distance}";
                return false;
            }

            return gridPlanBuilder.TryBuild(
                standingDice.CurrentState,
                direction,
                distance,
                context,
                out plan,
                out rejectReason);
        }

        bool TryEvaluate(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            bool allowTierLanding,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            var isJumping = context.IsJumping;
            var mode = JumpPlayerTransferPolicy.ResolveStandingMoveMode(isJumping, standingDice);

            switch (mode) {
                case DiceStandingMoveMode.Slide:
                    return TryEvaluateSlide(
                        fromCell,
                        toCell,
                        fromLevel,
                        fromSurface,
                        standingDice,
                        direction,
                        context,
                        allowTierLanding,
                        reach,
                        out transition);

                case DiceStandingMoveMode.Roll:
                    return TryEvaluateRoll(
                        fromCell,
                        toCell,
                        fromLevel,
                        fromSurface,
                        standingDice,
                        direction,
                        context,
                        allowTierLanding,
                        reach,
                        out transition);

                default:
                    return false;
            }
        }

        bool TryEvaluateSlide(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            bool allowTierLanding,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            var isJumping = context.IsJumping;

            if (!isJumping
                && TryBuildIceSlide(standingDice, fromLevel, direction, out var iceSlidePlan)) {
                transition = MovementTransition.IceSlide(iceSlidePlan);
                return true;
            }

            if (isJumping
                && JumpGridRollPolicy.TryCreateCoupledTransition(
                    fromCell,
                    toCell,
                    fromSurface,
                    standingDice,
                    direction,
                    context,
                    gridPlanBuilder,
                    out transition)) {
                return true;
            }

            if (allowTierLanding
                && TierLandingPolicy.TryEvaluate(
                    fromCell,
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    context,
                    registry,
                    reach,
                    out transition)) {
                return true;
            }

            return false;
        }

        bool TryEvaluateRoll(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            bool allowTierLanding,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            var isJumping = context.IsJumping;

            if (isJumping
                && JumpGridRollPolicy.TryCreateCoupledTransition(
                    fromCell,
                    toCell,
                    fromSurface,
                    standingDice,
                    direction,
                    context,
                    gridPlanBuilder,
                    out transition)) {
                return true;
            }

            if (!allowTierLanding
                && TopFallPolicy.TryEvaluate(
                    fromLevel,
                    fromSurface,
                    standingDice,
                    direction,
                    context,
                    gridPlanBuilder,
                    out transition)) {
                return true;
            }

            if (allowTierLanding
                && TierLandingPolicy.TryEvaluate(
                    fromCell,
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    context,
                    registry,
                    reach,
                    out transition)) {
                return true;
            }

            if (!TryEvaluateGridRoll(
                fromCell,
                toCell,
                fromSurface,
                standingDice,
                direction,
                MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell),
                allowMultiCell: false,
                context,
                out var gridPlan,
                out _)) {
                return false;
            }

            if (isJumping) {
                if (!context.AllowJumpGridMove) {
                    transition = MovementTransition.Blocked();
                    return true;
                }

                transition = CreateCoupledGridMoveTransition(standingDice, gridPlan);
                return true;
            }

            transition = MovementTransition.GridRoll(gridPlan);
            return true;
        }

        bool TryBuildIceSlide(
            DiceController standingDice,
            int fromLevel,
            Direction direction,
            out DiceSlidePlan plan) {
            plan = default;

            if (standingDice == null) {
                return false;
            }

            if (!standingDice.Capabilities.SlideUntilBlocked) {
                return false;
            }

            if (SurfaceHeightLevel.ToDiceStackTier(fromLevel) != standingDice.CurrentState.Tier) {
                return false;
            }

            if (!standingDice.IsPlayerMovable) {
                return false;
            }

            return IceSlidePassability.TryBuildUntilBlocked(
                standingDice.CurrentState,
                direction,
                registry,
                out plan,
                out _);
        }

        static MovementTransition CreateCoupledGridMoveTransition(
            DiceController standingDice,
            DiceGridMovePlan plan) {
            var targetLevel = SurfaceHeightLevel.FromDiceStackTier(plan.To.Tier);
            return MovementTransition.WalkableWithGridPlan(
                standingDice,
                targetLevel,
                MovementTransitionRoute.CoupledGridMove,
                plan);
        }
    }
}
