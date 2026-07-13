using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public enum WorkDieRollExecutionMode
    {
        GroundRoll,
        JumpRoll
    }

    public readonly struct WorkDieRollStep
    {
        public Direction Direction { get; }
        public Vector2Int LandingCell { get; }
        public DiceStackTier LandingTier { get; }
        public WorkDieRollExecutionMode Mode { get; }
        public MovementTransitionKind EdgeKind { get; }

        public WorkDieRollStep(
            Direction direction,
            Vector2Int landingCell,
            DiceStackTier landingTier,
            WorkDieRollExecutionMode mode,
            MovementTransitionKind edgeKind) {
            Direction = direction;
            LandingCell = landingCell;
            LandingTier = landingTier;
            Mode = mode;
            EdgeKind = edgeKind;
        }
    }

    public static class WorkDieRollPlanner
    {
        public static bool TrySelectRollStep(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            Direction direction,
            bool allowJump,
            out WorkDieRollStep step) {
            step = default;
            if (passability == null || workDie == null) {
                return false;
            }

            var fromCell = workDie.CurrentState.GridPos;
            var landingCell = fromCell + direction.ToGridDelta();
            var groundContext = PassabilityContext.ForGround(footingWorldY, movementOwner);
            var jumpContext = PassabilityContext.Jump(true, true, footingWorldY, movementOwner);

            var groundTransition = passability.Evaluate(
                fromCell,
                fromLevel,
                direction,
                workDie,
                groundContext);

            if (TryResolveGroundRoll(groundTransition, landingCell, out var groundLandingTier, out var groundEdgeKind)) {
                step = new WorkDieRollStep(
                    direction,
                    landingCell,
                    groundLandingTier,
                    WorkDieRollExecutionMode.GroundRoll,
                    groundEdgeKind);
                return true;
            }

            if (!allowJump || !workDie.CanJumpCoupleWithPlayer) {
                return false;
            }

            // Ground already resolved a dice grid move (CanRoll / TopFall). Do not escalate to JumpRoll.
            if (groundTransition.HasDiceGridMovePlan) {
                return false;
            }

            var jumpTransition = passability.Evaluate(
                fromCell,
                fromLevel,
                direction,
                workDie,
                jumpContext);

            if (TryResolveJumpRoll(jumpTransition, landingCell, out var jumpLandingTier)) {
                step = new WorkDieRollStep(
                    direction,
                    landingCell,
                    jumpLandingTier,
                    WorkDieRollExecutionMode.JumpRoll,
                    jumpTransition.Kind);
                return true;
            }

            return false;
        }

        public static bool TryResolveGroundRoll(
            MovementTransition transition,
            Vector2Int landingCell,
            out DiceStackTier landingTier,
            out MovementTransitionKind edgeKind) {
            landingTier = default;
            edgeKind = default;
            if (!transition.HasDiceGridMovePlan
                || transition.DiceGridMovePlan.To.GridPos != landingCell) {
                return false;
            }

            if (transition.Kind == MovementTransitionKind.CanRoll) {
                landingTier = transition.DiceGridMovePlan.To.Tier;
                edgeKind = MovementTransitionKind.CanRoll;
                return true;
            }

            // Top → Bottom demote roll (TopFall). Gameplay executes this without jump.
            if (transition.Kind == MovementTransitionKind.Walkable
                && transition.Route == MovementTransitionRoute.TopFall) {
                landingTier = transition.DiceGridMovePlan.To.Tier;
                edgeKind = MovementTransitionKind.Walkable;
                return true;
            }

            return false;
        }

        public static bool TryResolveJumpRoll(
            MovementTransition transition,
            Vector2Int landingCell,
            out DiceStackTier landingTier) {
            landingTier = default;
            if (!transition.HasDiceGridMovePlan
                || transition.DiceGridMovePlan.To.GridPos != landingCell) {
                return false;
            }

            if (transition.Kind == MovementTransitionKind.CanRoll) {
                landingTier = transition.DiceGridMovePlan.To.Tier;
                return true;
            }

            if (transition.Kind == MovementTransitionKind.Walkable
                && transition.Route == MovementTransitionRoute.CoupledGridMove) {
                landingTier = transition.DiceGridMovePlan.To.Tier;
                return true;
            }

            return false;
        }
    }
}
