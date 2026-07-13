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

        struct RollCancelSession
        {
            public bool IsActive;
            public DiceGridMovePlan OriginalPlan;
            public bool WasGroundRoll;
        }

        Board board;
        DiceRegistry registry;
        CharacterStandingController standing;
        CharacterTransformDriver transformDriver;
        CharacterMovementSettings movementSettings;
        PlayerMatchActionContext actionContext;
        PlayerSlot playerSlot;
        Func<float> getJumpYOffset;
        Func<VerticalMotionState> getJumpMotion;

        Session session;
        RollCancelSession rollCancelSession;

        public bool IsActive => session.IsActive;
        public bool IsTrackingRoll => session.IsTracking;
        public bool IsJumpArc => session.IsJumpArc;
        public bool JumpDiceGridMoved => session.JumpDiceGridMoved;
        public JumpDiceMoveKind JumpMoveKind => session.JumpMoveKind;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            CharacterStandingController standingController,
            CharacterTransformDriver driver,
            CharacterMovementSettings movement,
            Func<float> jumpYOffsetProvider,
            Func<VerticalMotionState> jumpMotionProvider,
            PlayerMatchActionContext targetActionContext,
            PlayerSlot owner) {
            board = targetBoard;
            registry = targetRegistry;
            standing = standingController;
            transformDriver = driver;
            movementSettings = movement;
            getJumpYOffset = jumpYOffsetProvider;
            getJumpMotion = jumpMotionProvider;
            actionContext = targetActionContext;
            playerSlot = owner;
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
            if (!session.IsTracking || dice == null || dice.IsMotionFollowActive) {
                return false;
            }

            var wasJumpArc = session.IsJumpArc;
            EndRollTracking();
            session.IsActive = false;
            session.IsJumpArc = false;
            ClearRollCancelSession();
            return wasJumpArc;
        }

        /// <summary>
        /// Start visual follow for a newly mounted standing dice (spawn/emergence/roll).
        /// Reuses the same anchor tracking as roll coupling.
        /// </summary>
        public void BeginMountFollow() {
            if (standing.CurrentDice?.View.DiceTransform == null) {
                Debug.LogError("DiceCharacterCoupling: BeginMountFollow requires a standing dice with a visual.");
                return;
            }

            ClearRollCancelSession();
            session.JumpDiceGridMoved = false;
            var diceCenter = standing.CurrentDice.View.DiceTransform.position;
            var charPos = transformDriver.GetWorldXZ();
            BeginFollow(
                new Vector3(charPos.x, 0f, charPos.y),
                diceCenter,
                jumpArc: false,
                JumpDiceMoveKind.None);
        }

        public bool TryHandleRollCancel(
            Vector2 input,
            bool jumpPressed,
            float passabilityReachY,
            MovementTransitionEvaluator movementTransition,
            Vector2 nextXZ,
            float halfExtent,
            Action beginJumpFromRollCancel,
            Action revertJumpFromRollCancel) {
            if (!rollCancelSession.IsActive || movementTransition == null) {
                return false;
            }

            var rollProgress = standing.CurrentDice != null
                ? standing.CurrentDice.GroundRollProgress
                : 0f;
            var cancelKind = RollCancelPolicy.Evaluate(
                rollCancelSession.OriginalPlan,
                rollProgress,
                movementSettings.RollCancelWindowProgress,
                input,
                jumpPressed,
                rollCancelSession.WasGroundRoll);
            if (cancelKind == RollCancelKind.None) {
                return false;
            }

            return cancelKind switch {
                RollCancelKind.Reverse => TryExecuteReverseRollCancel(
                    rollProgress,
                    movementTransition,
                    passabilityReachY,
                    nextXZ,
                    halfExtent),
                RollCancelKind.SwitchToJump => TryExecuteJumpSwitchRollCancel(
                    movementTransition,
                    passabilityReachY,
                    nextXZ,
                    halfExtent,
                    beginJumpFromRollCancel,
                    revertJumpFromRollCancel),
                _ => false
            };
        }

        public bool TryBeginGroundParallelRoll(DiceGridMovePlan plan, Vector2 nextXZ, float halfExtent) {
            return TryBeginGridMovePlan(plan, jumpArc: false, nextXZ, halfExtent);
        }

        public bool TryBeginGroundTopFallRoll(DiceGridMovePlan plan, Vector2 nextXZ, float halfExtent) {
            return TryBeginGridMovePlan(plan, jumpArc: false, nextXZ, halfExtent);
        }

        public bool TryBeginJumpGridMove(
            DiceGridMovePlan plan,
            Vector2 nextXZ,
            float halfExtent,
            Action<string> log) {
            if (!CanJumpCoupleWithStandingDice()) {
                return false;
            }

            if (session.JumpDiceGridMoved) {
                return false;
            }

            if (!TryBeginGridMovePlan(plan, jumpArc: true, nextXZ, halfExtent)) {
                log?.Invoke($"Coupling jump-grid reject execute-failed kind={plan.Kind}");
                return false;
            }

            return true;
        }

        public bool TryBeginJumpTopFallRoll(DiceGridMovePlan plan, Vector2 nextXZ, float halfExtent) {
            if (!CanJumpCoupleWithStandingDice()) {
                return false;
            }

            if (session.JumpDiceGridMoved) {
                return false;
            }

            return TryBeginGridMovePlan(plan, jumpArc: true, nextXZ, halfExtent);
        }

        public bool TryBeginJumpGridMoveForTransition(
            DiceGridMovePlan plan,
            Vector2 nextXZ,
            float halfExtent,
            Action<string> log) {
            return TryBeginJumpGridMove(plan, nextXZ, halfExtent, log);
        }

        void RegisterStandingDiceForAction() {
            actionContext?.RegisterActionDice(standing.CurrentDice, playerSlot);
        }

        bool TryExecuteReverseRollCancel(
            float cancelProgress,
            MovementTransitionEvaluator movementTransition,
            float passabilityReachY,
            Vector2 nextXZ,
            float halfExtent) {
            var dice = standing.CurrentDice;
            if (dice == null) {
                return false;
            }

            var originalPlan = rollCancelSession.OriginalPlan;
            if (!dice.TryInterruptActiveRoll(out var snapshot)) {
                return false;
            }

            var reverseDirection = originalPlan.Direction.Opposite();
            if (!movementTransition.TryBuildGridMovePlan(
                dice.CurrentState,
                reverseDirection,
                originalPlan.Distance,
                PassabilityContext.ForGround(passabilityReachY),
                out var reversePlan,
                out _)) {
                dice.View.SnapTo(dice.CurrentState, board, registry);
                return false;
            }

            ClearRollCancelSession();

            RegisterStandingDiceForAction();
            if (!dice.TryExecuteCancelReverseGroundMovePlan(reversePlan, snapshot, cancelProgress)) {
                Debug.LogError(
                    $"DiceCharacterCoupling: reverse roll cancel execution failed " +
                    $"from={reversePlan.From.GridPos} to={reversePlan.To.GridPos}");
                dice.View.SnapTo(dice.CurrentState, board, registry);
                return false;
            }

            FinalizeCancelMoveFollow(dice, reversePlan.To, jumpArc: false, nextXZ, halfExtent);
            return true;
        }

        bool TryExecuteJumpSwitchRollCancel(
            MovementTransitionEvaluator movementTransition,
            float passabilityReachY,
            Vector2 nextXZ,
            float halfExtent,
            Action beginJumpFromRollCancel,
            Action revertJumpFromRollCancel) {
            if (!rollCancelSession.WasGroundRoll) {
                return false;
            }

            var dice = standing.CurrentDice;
            if (dice == null || !CanJumpCoupleWithStandingDice()) {
                return false;
            }

            var originalPlan = rollCancelSession.OriginalPlan;
            if (!dice.TryInterruptActiveRoll(out var snapshot)) {
                return false;
            }

            if (!dice.RollbackLogicalStateOnly(originalPlan.From)) {
                return false;
            }

            standing.SetOnDice(originalPlan.From.GridPos, originalPlan.From.Tier, dice);

            if (!TryBuildJumpSwitchRollCancelPlan(
                movementTransition,
                originalPlan,
                passabilityReachY,
                out var jumpPlan,
                out var rejectReason)) {
                Debug.LogError(
                    $"DiceCharacterCoupling: jump-switch roll cancel plan rejected " +
                    $"from={originalPlan.From.GridPos} dir={originalPlan.Direction} {rejectReason}");
                dice.View.SnapTo(dice.CurrentState, board, registry);
                return false;
            }

            beginJumpFromRollCancel?.Invoke();
            ClearRollCancelSession();

            session.IsJumpArc = true;
            RegisterStandingDiceForAction();
            if (!dice.TryExecuteCancelJumpMovePlan(
                jumpPlan,
                snapshot,
                getJumpMotion)) {
                session.IsJumpArc = false;
                revertJumpFromRollCancel?.Invoke();
                Debug.LogError(
                    $"DiceCharacterCoupling: jump-switch roll cancel execution failed " +
                    $"from={jumpPlan.From.GridPos} to={jumpPlan.To.GridPos}");
                return false;
            }

            session.JumpDiceGridMoved = true;
            FinalizeCancelMoveFollow(dice, jumpPlan.To, jumpArc: true, nextXZ, halfExtent, jumpPlan.Kind);
            return true;
        }

        void FinalizeCancelMoveFollow(
            DiceController dice,
            DiceState toState,
            bool jumpArc,
            Vector2 nextXZ,
            float halfExtent,
            DiceGridMoveKind moveKind = DiceGridMoveKind.Parallel) {
            session.JumpMoveKind = moveKind switch {
                DiceGridMoveKind.Parallel => JumpDiceMoveKind.SameTierParallel,
                DiceGridMoveKind.Stack => JumpDiceMoveKind.StackOntoTop,
                DiceGridMoveKind.Demote => JumpDiceMoveKind.DemoteToBottom,
                _ => JumpDiceMoveKind.None
            };
            standing.SetOnDice(toState.GridPos, toState.Tier, dice);

            transformDriver.AlignToDiceFace(dice, nextXZ, halfExtent);
            var diceCenter = dice.View.DiceTransform.position;
            var charPos = transformDriver.GetWorldXZ();
            BeginFollow(new Vector3(charPos.x, 0f, charPos.y), diceCenter, jumpArc, session.JumpMoveKind);
            session.IsActive = true;
        }

        static bool TryBuildJumpSwitchRollCancelPlan(
            MovementTransitionEvaluator movementTransition,
            DiceGridMovePlan originalPlan,
            float passabilityReachY,
            out DiceGridMovePlan jumpPlan,
            out string rejectReason) {
            jumpPlan = default;
            rejectReason = null;

            var passability = PassabilityContext.Jump(
                allowJumpGridMove: true,
                allowJumpTierChange: true,
                passabilityReachY);

            for (var distance = DiceGridRollLimits.MaxParallelRollDistance; distance >= 1; distance--) {
                if (movementTransition.TryBuildGridMovePlan(
                    originalPlan.From,
                    originalPlan.Direction,
                    distance,
                    passability,
                    out jumpPlan,
                    out rejectReason)) {
                    return true;
                }
            }

            return false;
        }

        public bool TryBeginGroundIceSlide(DiceSlidePlan plan, Vector2 nextXZ, float halfExtent) {
            var dice = standing.CurrentDice;
            if (dice?.View.DiceTransform == null || dice.IsBusy) {
                return false;
            }

            RegisterStandingDiceForAction();
            if (!dice.TryExecuteSlidePlan(plan, playerSlot)) {
                Debug.LogError(
                    $"DiceCharacterCoupling: ice slide execution failed " +
                    $"from={plan.From.GridPos} to={plan.To.GridPos}");
                return false;
            }

            standing.SetOnDice(plan.To.GridPos, plan.To.Tier, dice);
            transformDriver.AlignToDiceFace(dice, nextXZ, halfExtent);
            var diceCenter = dice.View.DiceTransform.position;
            var charPos = transformDriver.GetWorldXZ();
            BeginFollow(new Vector3(charPos.x, 0f, charPos.y), diceCenter, jumpArc: false, JumpDiceMoveKind.None);
            session.IsActive = true;
            return true;
        }

        bool TryBeginGridMovePlan(
            DiceGridMovePlan plan,
            bool jumpArc,
            Vector2 nextXZ,
            float halfExtent) {
            var dice = standing.CurrentDice;
            if (dice?.View.DiceTransform == null || dice.IsBusy) {
                return false;
            }

            RegisterStandingDiceForAction();
            if (jumpArc) {
                session.IsJumpArc = true;
                if (!dice.TryExecuteJumpMovePlan(plan, getJumpYOffset(), getJumpMotion)) {
                    session.IsJumpArc = false;
                    Debug.LogError(
                        $"DiceCharacterCoupling: jump grid move execution failed kind={plan.Kind} " +
                        $"from={plan.From.GridPos} to={plan.To.GridPos}");
                    return false;
                }

                session.JumpDiceGridMoved = true;
            } else if (!dice.TryExecuteGroundMovePlan(
                plan,
                PassabilityContext.ForGround(board.FloorSurfaceWorldY, playerSlot))) {
                Debug.LogError(
                    $"DiceCharacterCoupling: ground grid move execution failed kind={plan.Kind} " +
                    $"from={plan.From.GridPos} to={plan.To.GridPos}");
                return false;
            }

            session.JumpMoveKind = plan.Kind switch {
                DiceGridMoveKind.Parallel => JumpDiceMoveKind.SameTierParallel,
                DiceGridMoveKind.Stack => JumpDiceMoveKind.StackOntoTop,
                DiceGridMoveKind.Demote => JumpDiceMoveKind.DemoteToBottom,
                _ => JumpDiceMoveKind.None
            };
            standing.SetOnDice(plan.To.GridPos, plan.To.Tier, dice);

            transformDriver.AlignToDiceFace(dice, nextXZ, halfExtent);
            var diceCenter = dice.View.DiceTransform.position;
            var charPos = transformDriver.GetWorldXZ();
            BeginFollow(new Vector3(charPos.x, 0f, charPos.y), diceCenter, jumpArc, session.JumpMoveKind);
            session.IsActive = true;

            if (!jumpArc && RollCancelPolicy.IsCancelEligiblePlan(plan)) {
                BeginRollCancelSession(plan, wasGroundRoll: true);
            }

            return true;
        }

        void BeginRollCancelSession(DiceGridMovePlan plan, bool wasGroundRoll) {
            rollCancelSession = new RollCancelSession {
                IsActive = true,
                OriginalPlan = plan,
                WasGroundRoll = wasGroundRoll
            };
        }

        void ClearRollCancelSession() {
            rollCancelSession = default;
        }

        bool CanJumpCoupleWithStandingDice() {
            return standing.CurrentDice == null
                || standing.CurrentDice.CanJumpCoupleWithPlayer;
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
