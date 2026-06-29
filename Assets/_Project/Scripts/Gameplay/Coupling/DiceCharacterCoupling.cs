using System;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Character;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.Coupling
{
    public enum JumpDiceMoveKind
    {
        None,
        SameTierParallel,
        StackOntoTop,
        DemoteToBottom
    }

    public sealed class DiceCharacterCoupling
    {
        struct Session
        {
            public bool IsActive;
            public bool IsTracking;
            public bool IsJumpArc;
            public bool JumpDiceGridMoved;
            public JumpDiceMoveKind JumpMoveKind;
            public Vector3 CharacterAnchor;
            public Vector3 DiceCenterAnchor;
        }

        Board board;
        DiceRegistry registry;
        MovementTransitionEvaluator passability;
        CharacterStandingController standing;
        CharacterTransformDriver transformDriver;
        CharacterMovementSettings movementSettings;
        Func<float> getJumpYOffset;
        Func<VerticalMotionState> getJumpMotion;

        Session session;

        public bool IsActive => session.IsActive;
        public bool IsTrackingRoll => session.IsTracking;
        public bool IsJumpArc => session.IsJumpArc;
        public bool JumpDiceGridMoved => session.JumpDiceGridMoved;
        public JumpDiceMoveKind JumpMoveKind => session.JumpMoveKind;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            MovementTransitionEvaluator movementPassability,
            CharacterStandingController standingController,
            CharacterTransformDriver driver,
            CharacterMovementSettings movement,
            Func<float> jumpYOffsetProvider,
            Func<VerticalMotionState> jumpMotionProvider) {
            board = targetBoard;
            registry = targetRegistry;
            passability = movementPassability;
            standing = standingController;
            transformDriver = driver;
            movementSettings = movement;
            getJumpYOffset = jumpYOffsetProvider;
            getJumpMotion = jumpMotionProvider;
        }

        public void ResetJumpSessionFlags() {
            session.JumpDiceGridMoved = false;
            session.IsJumpArc = false;
            session.JumpMoveKind = JumpDiceMoveKind.None;
        }

        public void EndRollTracking() {
            if (!session.IsTracking) {
                return;
            }

            SyncVisual();
            session.IsTracking = false;
            transformDriver.SnapYToSurface();
        }

        public void SyncVisual() {
            if (!session.IsTracking || standing.CurrentDice?.View.DiceTransform == null) {
                return;
            }

            var dice = standing.CurrentDice;
            var diceCenter = dice.View.DiceTransform.position;
            var delta = diceCenter - session.DiceCenterAnchor;
            var worldPosition = session.CharacterAnchor + delta;
            worldPosition.y = dice.GetTopSurfaceWorldY() + movementSettings.CharacterHeightOffset;
            transformDriver.ApplyWorldPosition(worldPosition);
        }

        public bool CompleteRollIfFinished(DiceController dice) {
            if (!session.IsTracking || dice == null || dice.IsRolling) {
                return false;
            }

            var wasJumpArc = session.IsJumpArc;
            EndRollTracking();
            session.IsActive = false;
            session.IsJumpArc = false;
            return wasJumpArc;
        }

        public bool TryBeginGroundParallelRoll(Direction direction, Vector2 nextXZ, float halfExtent) {
            var dice = standing.CurrentDice;
            if (dice?.View.DiceTransform == null || dice.IsDissolving) {
                return false;
            }

            if (standing.Tier != dice.CurrentState.Tier) {
                return false;
            }

            if (standing.Tier == DiceStackTier.Bottom && registry.HasTopAt(standing.GridCell)) {
                return false;
            }

            var hasTopOnSameCell = registry.HasTopAt(standing.GridCell);
            if (!RollResolver.TryRoll(
                dice.CurrentState,
                direction,
                registry,
                hasTopOnSameCell,
                out _)) {
                return false;
            }

            transformDriver.AlignToDiceFace(dice, nextXZ, halfExtent);
            var characterAnchor = transformDriver.GetWorldXZ();
            var diceCenter = dice.View.DiceTransform.position;

            if (!dice.TryRoll(direction)) {
                return false;
            }

            standing.SetOnDice(dice.CurrentState.GridPos, standing.Tier, dice);
            BeginFollow(new Vector3(characterAnchor.x, 0f, characterAnchor.y), diceCenter, false, JumpDiceMoveKind.None);
            return true;
        }

        public bool TryBeginGroundTopFallRoll(Direction direction, Vector2 nextXZ, float halfExtent) {
            var dice = standing.CurrentDice;
            if (dice?.View.DiceTransform == null || dice.IsDissolving) {
                return false;
            }

            if (standing.Tier != DiceStackTier.Top || dice.CurrentState.Tier != DiceStackTier.Top) {
                return false;
            }

            var targetPos = standing.GridCell + direction.ToGridDelta();
            if (!registry.CanPlaceBottomDiceAt(targetPos)) {
                return false;
            }

            transformDriver.AlignToDiceFace(dice, nextXZ, halfExtent);
            var characterAnchor = transformDriver.GetWorldXZ();
            var diceCenter = dice.View.DiceTransform.position;

            if (!dice.TryRollThenDemote(direction)) {
                return false;
            }

            standing.SetOnDice(dice.CurrentState.GridPos, DiceStackTier.Bottom, dice);
            BeginFollow(new Vector3(characterAnchor.x, 0f, characterAnchor.y), diceCenter, false, JumpDiceMoveKind.DemoteToBottom);
            return true;
        }

        public bool TryBeginJumpGridMove(
            Vector2Int fromCell,
            Vector2Int toCell,
            Direction direction,
            Vector2 nextXZ,
            float halfExtent,
            JumpCoupledMoveCapability capability,
            Action<string> log) {
            if (session.JumpDiceGridMoved) {
                return false;
            }

            var dice = standing.CurrentDice;
            if (dice == null || dice.IsDissolving) {
                return false;
            }

            var rollDistance = MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell);
            if (rollDistance < 1) {
                rollDistance = 1;
            }

            var jumpContext = PassabilityContext.Jump(
                capability.AllowDiceGridMove,
                capability.AllowTierChange,
                0f);
            if (!passability.TryBuildJumpGridMovePlan(
                dice.CurrentState,
                direction,
                rollDistance,
                jumpContext,
                out var plan,
                out _)) {
                log?.Invoke($"Coupling jump-grid reject plan-failed from=({fromCell.x},{fromCell.y}) to=({toCell.x},{toCell.y})");
                return false;
            }

            if (plan.ChangesTier && !capability.AllowTierChange) {
                return false;
            }

            session.IsJumpArc = true;
            if (!dice.TryExecuteJumpMovePlan(plan, getJumpYOffset(), getJumpMotion)) {
                session.IsJumpArc = false;
                log?.Invoke($"Coupling jump-grid reject execute-failed kind={plan.Kind}");
                return false;
            }

            session.JumpMoveKind = plan.Kind switch {
                DiceGridMoveKind.Parallel => JumpDiceMoveKind.SameTierParallel,
                DiceGridMoveKind.Stack => JumpDiceMoveKind.StackOntoTop,
                DiceGridMoveKind.Demote => JumpDiceMoveKind.DemoteToBottom,
                _ => JumpDiceMoveKind.None
            };
            session.JumpDiceGridMoved = true;
            standing.SetOnDice(plan.To.GridPos, plan.To.Tier, dice);

            transformDriver.AlignToDiceFace(dice, nextXZ, halfExtent);
            var diceCenter = dice.View.DiceTransform.position;
            var charPos = transformDriver.GetWorldXZ();
            BeginFollow(new Vector3(charPos.x, 0f, charPos.y), diceCenter, true, session.JumpMoveKind);
            session.IsActive = true;
            return true;
        }

        public bool TryBeginJumpTopFallRoll(Direction direction, Vector2 nextXZ, float halfExtent) {
            if (session.JumpDiceGridMoved) {
                return false;
            }

            var dice = standing.CurrentDice;
            if (dice?.View.DiceTransform == null || dice.IsDissolving) {
                return false;
            }

            if (standing.Tier != DiceStackTier.Top || dice.CurrentState.Tier != DiceStackTier.Top) {
                return false;
            }

            if (!SlideResolver.TrySlideTop(
                dice.CurrentState,
                direction,
                registry,
                out _,
                out var result)
                || result != TopSlideResult.FallToBottom) {
                return false;
            }

            if (!DiceGridMovePlanner.TryBuildPlan(
                dice.CurrentState,
                direction,
                1,
                DiceStackTier.Bottom,
                DiceGridMoveKind.Demote,
                out var plan,
                out _)) {
                return false;
            }

            session.IsJumpArc = true;
            if (!dice.TryExecuteJumpMovePlan(plan, getJumpYOffset(), getJumpMotion)) {
                session.IsJumpArc = false;
                return false;
            }

            session.JumpDiceGridMoved = true;
            session.JumpMoveKind = JumpDiceMoveKind.DemoteToBottom;
            standing.SetOnDice(plan.To.GridPos, plan.To.Tier, dice);

            transformDriver.AlignToDiceFace(dice, nextXZ, halfExtent);
            var diceCenter = dice.View.DiceTransform.position;
            var charPos = transformDriver.GetWorldXZ();
            BeginFollow(new Vector3(charPos.x, 0f, charPos.y), diceCenter, true, session.JumpMoveKind);
            session.IsActive = true;
            return true;
        }

        public bool TryBeginJumpGridMoveForTransition(
            Vector2Int fromCell,
            Vector2Int toCell,
            MovementTransition transition,
            Direction direction,
            Vector2 nextXZ,
            float halfExtent,
            JumpCoupledMoveCapability capability,
            Action<string> log) {
            if (transition.TargetDice != standing.CurrentDice) {
                return false;
            }

            return TryBeginJumpGridMove(fromCell, toCell, direction, nextXZ, halfExtent, capability, log);
        }

        void BeginFollow(Vector3 characterAnchor, Vector3 diceCenterAnchor, bool jumpArc, JumpDiceMoveKind moveKind) {
            session.IsActive = true;
            session.IsTracking = true;
            session.IsJumpArc = jumpArc;
            session.JumpMoveKind = moveKind;
            session.CharacterAnchor = characterAnchor;
            session.DiceCenterAnchor = diceCenterAnchor;
        }

        public void EnsureTrackingFromCurrentPose() {
            if (session.IsTracking || standing.CurrentDice?.View.DiceTransform == null) {
                return;
            }

            var diceCenter = standing.CurrentDice.View.DiceTransform.position;
            var charPos = transformDriver.GetWorldXZ();
            BeginFollow(new Vector3(charPos.x, 0f, charPos.y), diceCenter, session.IsJumpArc, session.JumpMoveKind);
        }
    }
}
