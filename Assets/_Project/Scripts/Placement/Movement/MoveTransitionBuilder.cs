using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Builds <see cref="MovementTransition"/> for a selected <see cref="MoveAction"/>.
    /// On builder failure: Blocked (no silent fall-through to another action).
    /// </summary>
    public sealed class MoveTransitionBuilder
    {
        readonly DiceRegistry registry;
        readonly HeightTransferEvaluator heightTransferEvaluator;

        public MoveTransitionBuilder(
            DiceRegistry registry,
            HeightTransferEvaluator heightTransferEvaluator) {
            this.registry = registry;
            this.heightTransferEvaluator = heightTransferEvaluator;
        }

        public MovementTransition Build(MoveAction action, in MoveFacts f) {
            switch (action) {
                case MoveAction.Blocked:
                    return MovementTransition.Blocked();

                case MoveAction.ExpandedFootprintWalk:
                    return f.ExpandedFootprintTransition;

                case MoveAction.TierLanding:
                    return f.CanTierLand
                        ? f.TierLandingTransition
                        : MovementTransition.Blocked();

                case MoveAction.IceSlide:
                    return f.HasIceSlideDisplacement
                        ? MovementTransition.IceSlide(f.IceSlidePlan, f.IceElasticTarget)
                        : MovementTransition.Blocked();

                case MoveAction.CoupledJumpGrid:
                    return BuildCoupledJumpGrid(f);

                case MoveAction.TopFall:
                    return f.CanTopFall
                        ? f.TopFallTransition
                        : MovementTransition.Blocked();

                case MoveAction.GridRoll:
                    return f.CanGridRoll
                        ? MovementTransition.GridRoll(f.GridRollPlan)
                        : MovementTransition.Blocked();

                case MoveAction.PlayerWalk:
                    return BuildPlayerWalk(f);

                case MoveAction.PlayerWalkFloor:
                    return BuildPlayerWalkFloor(f);

                case MoveAction.FloorToBottomMount:
                    return BuildFloorToBottom(f);

                case MoveAction.HeightTransfer:
                    return heightTransferEvaluator.Evaluate(
                        f.FromCell,
                        f.ToCell,
                        f.FromLevel,
                        f.FromSurface,
                        f.StandingDice,
                        f.Direction,
                        f.IsJumping,
                        f.Context.AllowJumpGridMove,
                        f.Reach);

                case MoveAction.ContinueToLanding:
                    Debug.LogError(
                        "[MoveTransitionBuilder] ContinueToLanding must be resolved by MoveActionSelector.");
                    return MovementTransition.Blocked();

                default:
                    Debug.LogError($"[MoveTransitionBuilder] Unhandled MoveAction={action}");
                    return MovementTransition.Blocked();
            }
        }

        static MovementTransition BuildCoupledJumpGrid(in MoveFacts f) {
            if (f.CanJumpGridRoll) {
                return f.JumpGridTransition;
            }

            if (f.CanGridRoll) {
                var targetLevel = SurfaceHeightLevel.FromDiceStackTier(f.GridRollPlan.To.Tier);
                return MovementTransition.WalkableWithGridPlan(
                    f.StandingDice,
                    targetLevel,
                    MovementTransitionRoute.CoupledGridMove,
                    f.GridRollPlan);
            }

            Debug.LogError(
                "[MoveTransitionBuilder] CoupledJumpGrid selected without jump-grid or grid-roll plan.");
            return MovementTransition.Blocked();
        }

        static MovementTransition BuildPlayerWalk(in MoveFacts f) {
            return MovementTransition.Walkable(
                f.TargetDice,
                f.TargetLevel,
                MovementTransitionRoute.HeightTransfer);
        }

        MovementTransition BuildPlayerWalkFloor(in MoveFacts f) {
            if (JumpPlayerTransferPolicy.CanUsePlayerOnlyLowerLevelJump(f.IsJumping, f.StandingDice)
                && f.Relation == MoveLevelRelation.Descent) {
                return MovementTransition.Walkable(
                    null,
                    SurfaceHeightLevel.Floor,
                    MovementTransitionRoute.FloorTransfer);
            }

            var allowDescentOnly = f.IsJumping
                || (f.Mode == DiceStandingMoveMode.PlayerOnly && f.Relation == MoveLevelRelation.Descent);

            return WalkTransferPolicy.EvaluateFloor(
                f.FromSurface,
                f.StandingDice,
                registry,
                f.Reach,
                allowDescentOnly);
        }

        MovementTransition BuildFloorToBottom(in MoveFacts f) {
            if (f.FloorMountBottomDice == null
                || !WalkTransferPolicy.TryEvaluateFloorToBottom(
                    f.FromSurface,
                    f.FloorMountBottomDice,
                    registry,
                    f.Reach,
                    out var transition)) {
                return MovementTransition.Blocked();
            }

            return transition;
        }
    }
}
