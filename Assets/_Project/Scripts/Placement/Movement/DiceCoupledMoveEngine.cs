using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// L2 dice-coupled probes and grid-roll helpers.
    /// Action selection lives in <see cref="MoveActionSelector"/>; this type only builds plans.
    /// </summary>
    public sealed class DiceCoupledMoveEngine
    {
        readonly DiceRegistry registry;
        readonly GridMovePlanBuilder gridPlanBuilder;

        public DiceCoupledMoveEngine(DiceRegistry registry, GridMovePlanBuilder gridPlanBuilder) {
            this.registry = registry;
            this.gridPlanBuilder = gridPlanBuilder;
        }

        public GridMovePlanBuilder PlanBuilder => gridPlanBuilder;

        /// <summary>
        /// Standing-move ice probe: displacement required.
        /// Immediate block is not a slide success (HeightTransfer owns same-tier ride).
        /// Push/elastic uses <see cref="IceSlidePassability"/> with allowElasticOnImmediateBlock separately.
        /// </summary>
        public bool TryProbeIceSlide(
            DiceController standingDice,
            int fromLevel,
            Direction direction,
            out DiceSlidePlan plan,
            out DiceController elasticTransferTarget) {
            plan = default;
            elasticTransferTarget = null;

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

            if (!IceSlidePassability.TryBuildUntilBlocked(
                standingDice.CurrentState,
                direction,
                registry,
                out plan,
                out elasticTransferTarget,
                allowElasticOnImmediateBlock: false,
                out _)) {
                return false;
            }

            if (!standingDice.Capabilities.TransfersSlideOnCollision) {
                elasticTransferTarget = null;
            }

            return IceSlidePassability.HasSlideDisplacement(plan);
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
    }
}
