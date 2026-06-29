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
            CharacterStandingController standingController,
            CharacterTransformDriver driver,
            CharacterMovementSettings movement,
            Func<float> jumpYOffsetProvider,
            Func<VerticalMotionState> jumpMotionProvider) {
            board = targetBoard;
            registry = targetRegistry;
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

        bool TryBeginGridMovePlan(
            DiceGridMovePlan plan,
            bool jumpArc,
            Vector2 nextXZ,
            float halfExtent) {
            var dice = standing.CurrentDice;
            if (dice?.View.DiceTransform == null || dice.IsDissolving) {
                return false;
            }

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
            } else if (!dice.TryExecuteGroundMovePlan(plan)) {
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
            return true;
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
