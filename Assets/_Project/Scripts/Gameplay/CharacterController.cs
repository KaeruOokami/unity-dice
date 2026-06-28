using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class CharacterController : MonoBehaviour
    {
        const float EdgeEpsilon = 0.001f;

        [SerializeField] Board board;
        [SerializeField] GameObject characterObject;

        CharacterMovementSettings movementSettings;
        PhysicsSettings physicsSettings;

        const float MovementBlockLogInterval = 0.25f;
        const float PushDebugLogInterval = 0.25f;

        MovementTransitionEvaluator movementTransition;
        string debugLastMovementBlockKey;
        float debugLastMovementBlockLogTime = -1f;
        string debugLastPushKey;
        float debugLastPushLogTime = -1f;

        enum LiftPhase {
            None,
            Lifting,
            Carrying,
            Placing
        }

        enum JumpPhase {
            None,
            Airborne
        }

        enum JumpDiceMoveKind {
            None,
            SameTierParallel,
            StackOntoTop,
            DemoteToBottom
        }

        DiceRegistry registry;
        DiceController currentDice;
        SurfaceLayer standingSurfaceLayer;
        DiceStackTier standingTier;
        Vector2Int standingGridCell;
        Transform characterMount;
        Transform characterTransform;
        CapsuleCollider characterPushCollider;
        bool isTrackingDiceRoll;
        Vector3 rollStartCharacterPosition;
        Vector3 rollStartDiceCenter;
        float currentSpeed;
        float pushContactTime;
        Direction? dissolveDescentHoldDirection;
        float dissolveDescentHoldTime;
        DiceController pushTargetDice;
        Direction pushDirection;
        bool hasPushDirection;
        DiceController pushFollowDice;
        Direction pushFollowDirection;
        bool isPushFollowing;
        bool isInitialized;
        readonly List<PushContactCandidate> pushCandidates = new();
        LiftPhase liftPhase;
        DiceController carriedDice;
        Direction lastFacing;
        bool hasLastFacing;
        JumpPhase jumpPhase;
        VerticalMotionState jumpMotion;
        float jumpYOffset;
        DiceController jumpVisualDice;
        bool jumpDiceGridMoved;
        bool jumpArcRollActive;
        JumpDiceMoveKind jumpDiceMoveKind;
        bool hasPendingJumpDiceStandingUpdate;
        Vector2Int pendingJumpDiceToCell;
        DiceStackTier pendingJumpDiceTier;

        struct PushContactCandidate {
            public DiceController Dice;
            public Direction Direction;
            public float InputAlignment;
            public float FaceDistance;
        }

        public bool IsOnFloor => standingSurfaceLayer == SurfaceLayer.Floor;
        public bool IsBusy => !IsOnFloor && currentDice != null && currentDice.IsRolling;
        public bool IsCarrying => liftPhase != LiftPhase.None;
        public Vector2 FacePosition => TryGetStandingDice(out var standingDice)
            ? GetOffsetFromDiceCenter(standingDice, characterTransform != null ? characterTransform.position : Vector3.zero)
            : Vector2.zero;
        public DiceController CurrentDice => currentDice;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            DiceController startDice,
            CharacterMovementSettings movement,
            PhysicsSettings physics) {
            board = targetBoard;
            registry = targetRegistry;
            movementSettings = movement;
            physicsSettings = physics;
            if (startDice != null) {
                SetStandingOnDice(
                    startDice.CurrentState.GridPos,
                    startDice.CurrentState.Tier,
                    startDice);
            } else {
                SetStandingOnFloor(startGridCellFromTransform());
            }

            Initialize();
        }

        Vector2Int startGridCellFromTransform() {
            return characterTransform != null && board != null
                ? board.WorldToGrid(characterTransform.position)
                : Vector2Int.zero;
        }

        public void Initialize() {
            if (board == null || registry == null) {
                Debug.LogError("CharacterController: Board or DiceRegistry is not assigned.");
                return;
            }

            if (movementSettings == null || physicsSettings == null) {
                Debug.LogError("CharacterController: Movement or physics settings are not assigned.");
                return;
            }

            if (currentDice != null) {
                currentDice.View.EnsureDiceInstance();
                if (currentDice.View.DiceTransform == null) {
                    Debug.LogError("CharacterController: Dice visual is not available.");
                    return;
                }
            }

            EnsureCharacterInstance();
            EnsureCharacterPushCollider();
            movementTransition = new MovementTransitionEvaluator(board, registry, movementSettings.MaxStepHeight);
            currentSpeed = 0f;
            isInitialized = true;

            SyncStandingDiceCache();
            if (!IsOnFloor && TryGetStandingDice(out var startStanding) && startStanding.View.DiceTransform != null) {
                var center = startStanding.View.DiceTransform.position;
                ApplyWorldPosition(new Vector3(center.x, 0f, center.z));
            } else if (characterTransform != null) {
                standingGridCell = board.WorldToGrid(characterTransform.position);
                SnapYToSurface();
            } else {
                SnapYToSurface();
            }
        }

        public void OnStandingDiceDissolved(DiceController dissolvedDice) {
            if (!isInitialized || !TryGetStandingDice(out var standingDice) || standingDice != dissolvedDice) {
                return;
            }

            var grid = standingGridCell;
            if (standingTier == DiceStackTier.Top && registry.TryGetBottomAt(grid, out var bottom)) {
                SetStandingOnDice(grid, DiceStackTier.Bottom, bottom);
                SnapYToSurface();
                return;
            }

            if (standingTier == DiceStackTier.Bottom && registry.TryGetTopAt(grid, out var top)) {
                SetStandingOnDice(grid, DiceStackTier.Top, top);
                SnapYToSurface();
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        public void OnStandingDiceBecameGhost(DiceController ghostDice) {
            if (!isInitialized || !TryGetStandingDice(out var standingDice) || standingDice != ghostDice) {
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        void OnDisable() {
            EndRollTracking();
            EndCarryState();
            EndJump();
            UnsubscribeStandingDice();
            EndPushFollow();
        }

        void Update() {
            if (!isInitialized) {
                return;
            }

            var input = GetInputDirection();
            UpdateLastFacing(input);

            if (liftPhase == LiftPhase.Lifting || liftPhase == LiftPhase.Placing) {
                currentSpeed = 0f;
                return;
            }

            if (liftPhase == LiftPhase.Carrying) {
                currentSpeed = 0f;
                if (TryGetDirectionKeyDown(out var placeDirection)) {
                    TryPlaceCarriedDice(placeDirection);
                }

                return;
            }

            if (Input.GetKeyDown(movementSettings.LiftKey)) {
                TryBeginLift();
            }

            if (jumpPhase == JumpPhase.None && Input.GetKeyDown(movementSettings.JumpKey)) {
                TryBeginJump();
            }

            if (!IsOnFloor && currentDice != null && isTrackingDiceRoll && !currentDice.IsRolling) {
                var wasArcRoll = jumpArcRollActive;
                jumpArcRollActive = false;
                EndRollTracking();
                CompletePendingJumpDiceStandingUpdate();
                if (jumpDiceGridMoved && jumpDiceMoveKind == JumpDiceMoveKind.StackOntoTop) {
                    EndJump();
                } else if (wasArcRoll && jumpPhase != JumpPhase.None) {
                    EndJump();
                }
            }

            if (jumpPhase != JumpPhase.None) {
                UpdateJump();
                ApplyStandingDiceJumpVisual();
            }

            if (isPushFollowing) {
                currentSpeed = 0f;
                return;
            }

            var isRolling = !IsOnFloor && currentDice != null && currentDice.IsRolling;

            if (isRolling) {
                UpdateDuringRoll(input);
            } else {
                UpdateSurfaceMovement(input);
            }
        }

        void LateUpdate() {
            if (!isInitialized) {
                return;
            }

            if (isPushFollowing) {
                UpdatePushFollowPosition();
                if (pushFollowDice == null || !pushFollowDice.IsRolling) {
                    EndPushFollow();
                }
            }

            if (liftPhase == LiftPhase.Carrying && carriedDice != null) {
                carriedDice.View.SetCarryWorldPosition(GetCarryWorldPosition());
            }

            if (!IsOnFloor && currentDice != null && currentDice.IsRolling) {
                SyncPositionDuringRoll();
            } else if (!IsOnFloor || jumpPhase != JumpPhase.None) {
                SnapYToSurface();
            }
        }

        void UpdateSurfaceMovement(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                currentSpeed = 0f;
                ResetPushState();
                ResetDissolveDescentHold();
                return;
            }

            input.Normalize();
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                movementSettings.MaxMoveSpeed,
                movementSettings.MoveAcceleration * Time.deltaTime);

            if (currentSpeed <= 0f) {
                return;
            }

            var move = input * (currentSpeed * Time.deltaTime);
            SyncStandingDiceCache();
            var currentXZ = GetWorldXZ();
            var standingCell = GetStandingCell();
            var fromLayer = GetCurrentLayer();
            var fromSurfaceY = GetEffectiveSurfaceWorldY();
            var halfExtent = GetWalkHalfExtent();
            var nextXZ = currentXZ + move;

            if (IsOnFloor) {
                nextXZ = ClampToBoardBounds(nextXZ);
            }

            if (TryApplyPositionBasedMovement(
                currentXZ,
                ref nextXZ,
                move,
                standingCell,
                fromLayer,
                fromSurfaceY,
                halfExtent)) {
                return;
            }

            ApplyWorldPosition(new Vector3(nextXZ.x, 0f, nextXZ.y));
            UpdatePushContact(input);
        }

        bool TryApplyPositionBasedMovement(
            Vector2 currentXZ,
            ref Vector2 nextXZ,
            Vector2 move,
            Vector2Int standingCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            float halfExtent) {
            var nextCell = ResolveNextCell(standingCell, currentXZ, nextXZ, move, halfExtent);

            if (nextCell == standingCell) {
                nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);

                return false;
            }

            if (!MovementTransitionEvaluator.IsOrthogonalWithinDistance(
                standingCell,
                nextCell,
                GetMaxMovementCellDistance(standingCell, nextCell))) {
                nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);

                return false;
            }

            if (!MovementTransitionEvaluator.TryGetDirectionBetween(standingCell, nextCell, out var direction)) {
                nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);
                return false;
            }

            var standingDice = ResolveStandingDiceForMovement();
            var isJumping = jumpPhase != JumpPhase.None;
            var cellDistance = MovementTransitionEvaluator.GetOrthogonalDistance(standingCell, nextCell);
            var transition = cellDistance > 1
                ? movementTransition.EvaluateToTargetCell(
                    standingCell,
                    nextCell,
                    fromLayer,
                    fromSurfaceY,
                    standingDice,
                    standingTier,
                    ignoreStepHeight: isJumping,
                    isJumping: isJumping)
                : movementTransition.Evaluate(
                    standingCell,
                    fromLayer,
                    direction,
                    fromSurfaceY,
                    standingDice,
                    standingTier,
                    ignoreStepHeight: isJumping,
                    isJumping: isJumping);

            switch (transition.Kind) {
                case MovementTransitionKind.Walkable:
                    ResetDissolveDescentHold();
                    if (transition.TargetLayer == SurfaceLayer.Bottom
                        && standingTier == DiceStackTier.Top
                        && transition.TargetDice == currentDice
                        && TryGetPrimaryDirection(move, out var topFallDir)
                        && topFallDir == direction) {
                        if (jumpPhase == JumpPhase.None && TryExecuteTopFallRoll(direction, nextXZ, halfExtent)) {
                            return true;
                        }

                        if (jumpPhase != JumpPhase.None && TryExecuteJumpTopFallRoll(direction, nextXZ, halfExtent)) {
                            return true;
                        }
                    }

                    if (TryApplyJumpDiceMove(standingCell, nextCell, transition, direction, nextXZ, halfExtent)) {
                        return true;
                    }

                    if (ShouldBlockFailedJumpGridMoveFallback(isJumping, standingCell, nextCell)) {
                        nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);
                        return false;
                    }

                    if (isJumping
                        && IsJumpStackWalkableTransition(transition, standingTier)
                        && !CanPerformJumpStackMove()) {
                        nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);
                        return false;
                    }

                    ApplyTransitionStanding(transition, nextCell);
                    return false;
                case MovementTransitionKind.CanRoll:
                    if (jumpPhase != JumpPhase.None && currentDice != null) {
                        var jumpTargetLayer = standingTier == DiceStackTier.Top
                            ? SurfaceLayer.Top
                            : SurfaceLayer.Bottom;
                        var jumpDiceMove = MovementTransition.Walkable(currentDice, jumpTargetLayer);
                        if (TryApplyJumpDiceMove(standingCell, nextCell, jumpDiceMove, direction, nextXZ, halfExtent)) {
                            return true;
                        }

                        if (ShouldBlockFailedJumpGridMoveFallback(isJumping, standingCell, nextCell)) {
                            nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);
                            return false;
                        }
                    }

                    if (TryGetPrimaryDirection(move, out var moveDir) && moveDir == direction) {
                        if (TryExecuteGridRoll(direction, nextXZ, halfExtent)) {
                            UpdatePushContact(Vector2.zero);
                            return true;
                        }

                        if (ShouldBlockFailedJumpGridMoveFallback(isJumping, standingCell, nextCell)) {
                            nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);
                            return false;
                        }

                        if (Mathf.Abs(fromSurfaceY - board.FloorSurfaceWorldY) <= movementSettings.MaxStepHeight) {
                            ApplyTransitionStanding(MovementTransition.Walkable(null, SurfaceLayer.Floor), nextCell);
                            return false;
                        }

                        LogPositionMovementBlock(
                            "RollFailed",
                            standingCell,
                            nextCell,
                            fromLayer,
                            fromSurfaceY,
                            halfExtent,
                            currentXZ,
                            nextXZ,
                            move,
                            MovementTransitionKind.CanRoll,
                            "roll and step-to-floor both failed");
                    }

                    nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);
                    return false;
                case MovementTransitionKind.Blocked:
                    if (TryApplyDissolveDescentHold(
                        standingCell,
                        nextCell,
                        fromLayer,
                        fromSurfaceY,
                        move,
                        direction,
                        standingDice)) {
                        return false;
                    }

                    LogPositionMovementBlock(
                        "TransitionBlocked",
                        standingCell,
                        nextCell,
                        fromLayer,
                        fromSurfaceY,
                        halfExtent,
                        currentXZ,
                        nextXZ,
                        move,
                        transition.Kind,
                        $"stack={FormatMovementStack(nextCell)}");
                    nextXZ = CancelMoveIntoDirection(currentXZ, nextXZ, direction);
                    nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);

                    return false;
                default:
                    return false;
            }
        }

        void ResetDissolveDescentHold() {
            dissolveDescentHoldDirection = null;
            dissolveDescentHoldTime = 0f;
        }

        bool TryApplyDissolveDescentHold(
            Vector2Int standingCell,
            Vector2Int nextCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            Vector2 move,
            Direction direction,
            DiceController standingDice) {
            if (standingDice == null || !standingDice.IsDissolving) {
                ResetDissolveDescentHold();
                return false;
            }

            if (!TryGetPrimaryDirection(move, out var moveDir) || moveDir != direction) {
                ResetDissolveDescentHold();
                return false;
            }

            if (!movementTransition.IsDescentBlockedOnlyByStepHeight(
                standingCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standingTier)) {
                ResetDissolveDescentHold();
                return false;
            }

            if (dissolveDescentHoldDirection != direction) {
                dissolveDescentHoldDirection = direction;
                dissolveDescentHoldTime = 0f;
            }

            dissolveDescentHoldTime += Time.deltaTime;
            if (dissolveDescentHoldTime < movementSettings.DissolveDescentHoldDuration) {
                return false;
            }

            var transition = movementTransition.Evaluate(
                standingCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standingTier,
                ignoreStepHeight: true);
            if (transition.Kind != MovementTransitionKind.Walkable) {
                ResetDissolveDescentHold();
                return false;
            }

            ApplyTransitionStanding(transition, nextCell);
            ResetDissolveDescentHold();
            return true;
        }

        Vector2Int XZToGrid(Vector2 xz) {
            return board.WorldToGrid(new Vector3(xz.x, 0f, xz.y));
        }

        int GetMaxMovementCellDistance(Vector2Int standingCell, Vector2Int nextCell) {
            if (jumpPhase == JumpPhase.None) {
                return 1;
            }

            var distance = MovementTransitionEvaluator.GetOrthogonalDistance(standingCell, nextCell);
            return distance > 0 ? distance : 1;
        }

        int GetJumpParallelRollDistance() {
            if (!CanAttemptJumpGridMove()) {
                return 0;
            }

            var ratio = GetJumpHeightDiceRatio();
            var twoCellMax = physicsSettings.JumpParallelRollTwoCellMaxRatio;
            if (ratio <= twoCellMax + JumpHeightEpsilon) {
                return RollResolver.MaxParallelRollDistance;
            }

            if (ratio <= physicsSettings.JumpHeightDiceMultiplier + JumpHeightEpsilon) {
                return 1;
            }

            return 0;
        }

        const float JumpHeightEpsilon = 0.001f;

        Vector2Int ResolveNextCell(
            Vector2Int standingCell,
            Vector2 currentXZ,
            Vector2 nextXZ,
            Vector2 move,
            float halfExtent) {
            if (TryGetPrimaryDirection(move, out var moveDir)) {
                if (IsAtOrPastFaceEdge(currentXZ, standingCell, moveDir, halfExtent)) {
                    if (jumpPhase != JumpPhase.None && CanAttemptJumpGridMove()) {
                        if (IsJumpSameTierParallelRollDirection(standingCell, moveDir)) {
                            var rollDistance = GetJumpParallelRollDistance();
                            if (rollDistance >= 1
                                && movementTransition.TryGetJumpParallelRollTarget(
                                    standingCell,
                                    moveDir,
                                    ResolveStandingDiceForMovement(),
                                    standingTier,
                                    rollDistance,
                                    out var rollTarget,
                                    out _)) {
                                return rollTarget;
                            }

                            return standingCell;
                        }

                        return standingCell + moveDir.ToGridDelta();
                    }

                    return standingCell + moveDir.ToGridDelta();
                }

                return standingCell;
            }

            var positionCell = XZToGrid(nextXZ);
            if (positionCell == standingCell) {
                return standingCell;
            }

            if (MovementTransitionEvaluator.IsOrthogonalWithinDistance(
                standingCell,
                positionCell,
                1)) {
                return positionCell;
            }

            return standingCell;
        }

        bool IsAtOrPastFaceEdge(Vector2 xz, Vector2Int cell, Direction direction, float halfExtent) {
            var center = GetCellCenterXZ(cell);

            switch (direction) {
                case Direction.East:
                    return xz.x >= center.x + halfExtent - EdgeEpsilon;
                case Direction.West:
                    return xz.x <= center.x - halfExtent + EdgeEpsilon;
                case Direction.North:
                    return xz.y >= center.y + halfExtent - EdgeEpsilon;
                case Direction.South:
                    return xz.y <= center.y - halfExtent + EdgeEpsilon;
                default:
                    return false;
            }
        }

        void LogPositionMovementBlock(
            string reason,
            Vector2Int standingCell,
            Vector2Int nextCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            float halfExtent,
            Vector2 currentXZ,
            Vector2 nextXZ,
            Vector2 intendedMove,
            MovementTransitionKind transitionKind,
            string extra) {
            if (!movementSettings.DebugMovementBlock) {
                return;
            }

            if (!MovementTransitionEvaluator.TryGetDirectionBetween(standingCell, nextCell, out var direction)) {
                direction = Direction.North;
            }

            var standingDice = ResolveStandingDiceForMovement();
            var target = movementTransition.IsWalkableBetween(
                standingCell,
                nextCell,
                fromLayer,
                fromSurfaceY,
                standingDice,
                standingTier)
                ? DescribeWalkableTarget(standingCell, nextCell, fromLayer, fromSurfaceY)
                : "(none)";

            var detail =
                $"from={FormatMovementGrid(standingCell)} to={FormatMovementGrid(nextCell)} " +
                $"posCell={FormatMovementGrid(XZToGrid(nextXZ))} " +
                $"layer={fromLayer} tier={standingTier} dice={FormatMovementDice(standingDice)} " +
                $"target={target} stack={FormatMovementStack(nextCell)} " +
                $"transition={transitionKind} surfaceY={fromSurfaceY:F3} halfExtent={halfExtent:F3} " +
                $"pos={FormatMovementVector2(currentXZ)} final={FormatMovementVector2(nextXZ)} " +
                $"intended={FormatMovementVector2(intendedMove)} " +
                $"intendedLen={intendedMove.magnitude:F4} actualLen={(nextXZ - currentXZ).magnitude:F4} " +
                extra;

            LogMovementBlock(reason, direction, detail);
        }

        string DescribeWalkableTarget(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY) {
            if (!MovementTransitionEvaluator.TryGetDirectionBetween(fromCell, toCell, out var direction)) {
                return "(none)";
            }

            var standingDice = ResolveStandingDiceForMovement();
            var transition = movementTransition.Evaluate(
                fromCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standingTier);
            if (transition.TargetLayer == SurfaceLayer.Floor) {
                return "Floor";
            }

            return FormatMovementDice(transition.TargetDice);
        }

        void LogMovementBlock(string reason, Direction direction, string detail) {
            if (!movementSettings.DebugMovementBlock) {
                return;
            }

            var key = $"{reason}:{direction}";
            if (key == debugLastMovementBlockKey
                && Time.time - debugLastMovementBlockLogTime < MovementBlockLogInterval) {
                return;
            }

            debugLastMovementBlockKey = key;
            debugLastMovementBlockLogTime = Time.time;
            Debug.Log($"[MoveBlock] reason={reason} dir={direction} {detail}");
        }

        static string FormatMovementVector2(Vector2 value) {
            return $"({value.x:F3}, {value.y:F3})";
        }

        static string FormatMovementGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }

        string FormatMovementStack(Vector2Int gridPos) {
            if (registry == null) {
                return "Top=(none) Bottom=(none)";
            }

            registry.TryGetTopAt(gridPos, out var top);
            registry.TryGetBottomAt(gridPos, out var bottom);
            return $"Top={FormatMovementDice(top)} Bottom={FormatMovementDice(bottom)}";
        }

        static string FormatMovementDice(DiceController dice) {
            if (dice == null) {
                return "(none)";
            }

            var state = dice.CurrentState;
            return $"Grid({state.GridPos.x},{state.GridPos.y}) {state.Tier}";
        }

        Vector2Int GetStandingCell() {
            return standingGridCell;
        }

        DiceController ResolveStandingDiceForMovement() {
            SyncStandingDiceCache();
            return currentDice;
        }

        bool TryGetStandingDice(out DiceController dice) {
            dice = null;
            if (registry == null || standingSurfaceLayer == SurfaceLayer.Floor) {
                return false;
            }

            if (standingSurfaceLayer == SurfaceLayer.Top) {
                if (registry.TryGetTopAt(standingGridCell, out dice)) {
                    return true;
                }

                if (currentDice != null
                    && currentDice.CurrentState.Tier == DiceStackTier.Bottom
                    && currentDice.CurrentState.GridPos == standingGridCell) {
                    dice = currentDice;
                    return true;
                }

                return false;
            }

            return registry.TryGetBottomAt(standingGridCell, out dice);
        }

        void SyncStandingDiceCache() {
            if (!TryGetStandingDice(out var dice)) {
                if (currentDice != null) {
                    UnsubscribeStandingDice();
                    currentDice = null;
                }

                return;
            }

            if (currentDice != dice) {
                ResubscribeStandingDice(dice);
            }
        }

        void ApplyTransitionStanding(MovementTransition transition, Vector2Int toCell) {
            if (transition.TargetLayer == SurfaceLayer.Floor) {
                SetStandingOnFloor(toCell);
                return;
            }

            if (transition.TargetDice != null) {
                var tier = transition.TargetLayer == SurfaceLayer.Top
                    ? DiceStackTier.Top
                    : DiceStackTier.Bottom;
                SetStandingOnDice(toCell, tier, transition.TargetDice);
            }
        }

        bool ShouldBlockFailedJumpGridMoveFallback(
            bool isJumping,
            Vector2Int fromCell,
            Vector2Int toCell) {
            if (!isJumping) {
                return false;
            }

            return MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell) >= 1;
        }

        bool TryApplyJumpDiceMove(
            Vector2Int fromCell,
            Vector2Int toCell,
            MovementTransition transition,
            Direction direction,
            Vector2 nextXZ,
            float halfExtent) {
            if (!CanAttemptJumpGridMove()
                || currentDice == null
                || transition.TargetDice != currentDice
                || currentDice.IsDissolving) {
                return false;
            }

            JumpDiceMoveKind moveKind;
            DiceStackTier targetTier;
            if (!TryBeginJumpDiceRoll(
                fromCell,
                toCell,
                transition,
                direction,
                out moveKind,
                out targetTier)) {
                return false;
            }

            jumpDiceMoveKind = moveKind;
            QueuePendingJumpDiceStandingUpdate(toCell, targetTier);
            jumpDiceGridMoved = true;

            var diceCenter = currentDice.View.DiceTransform.position;
            var diceCenterAnchor = diceCenter;
            var nextOffset = WorldOffsetFromDiceCenter(diceCenter, nextXZ);
            var clamped = ClampToFace(nextOffset, halfExtent);
            ApplyWorldPosition(new Vector3(diceCenter.x + clamped.x, 0f, diceCenter.z + clamped.y));
            BeginRollTracking(characterTransform.position, diceCenterAnchor);
            return true;
        }

        bool TryBeginJumpDiceRoll(
            Vector2Int fromCell,
            Vector2Int toCell,
            MovementTransition transition,
            Direction direction,
            out JumpDiceMoveKind moveKind,
            out DiceStackTier targetTier) {
            moveKind = JumpDiceMoveKind.None;
            targetTier = standingTier;

            if (IsSameTierJumpParallelRoll(transition)) {
                var rollDistance = MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell);
                if (rollDistance < 1) {
                    rollDistance = 1;
                }

                var useArcRoll = rollDistance >= 2;
                if (useArcRoll) {
                    jumpArcRollActive = true;
                }

                if (!currentDice.TryJumpRoll(
                    direction,
                    jumpYOffset,
                    rollDistance,
                    useArcRoll ? () => jumpMotion : null)) {
                    jumpArcRollActive = false;
                    return false;
                }

                moveKind = JumpDiceMoveKind.SameTierParallel;
                return true;
            }

            if (standingTier == DiceStackTier.Top
                && standingSurfaceLayer == SurfaceLayer.Top
                && transition.TargetLayer == SurfaceLayer.Bottom) {
                if (!currentDice.TryJumpRollThenDemote(direction, jumpYOffset)) {
                    return false;
                }

                moveKind = JumpDiceMoveKind.DemoteToBottom;
                targetTier = DiceStackTier.Bottom;
                return true;
            }

            if (standingTier == DiceStackTier.Bottom
                && standingSurfaceLayer == SurfaceLayer.Bottom
                && transition.TargetLayer == SurfaceLayer.Top) {
                if (!CanPerformJumpStackMove() || !registry.CanPlaceTopDiceAt(toCell)) {
                    return false;
                }

                if (!currentDice.TryJumpStack(direction, jumpYOffset)) {
                    return false;
                }

                moveKind = JumpDiceMoveKind.StackOntoTop;
                targetTier = DiceStackTier.Top;
                return true;
            }

            return false;
        }

        void QueuePendingJumpDiceStandingUpdate(Vector2Int toCell, DiceStackTier tier) {
            hasPendingJumpDiceStandingUpdate = true;
            pendingJumpDiceToCell = toCell;
            pendingJumpDiceTier = tier;
        }

        void CompletePendingJumpDiceStandingUpdate() {
            if (!hasPendingJumpDiceStandingUpdate) {
                return;
            }

            hasPendingJumpDiceStandingUpdate = false;
            UpdateStandingAfterJumpDiceMove(pendingJumpDiceToCell, pendingJumpDiceTier);
        }

        void ClearPendingJumpDiceStandingUpdate() {
            hasPendingJumpDiceStandingUpdate = false;
        }

        bool IsJumpSameTierParallelRollDirection(Vector2Int standingCell, Direction direction) {
            SyncStandingDiceCache();
            if (currentDice == null) {
                return false;
            }

            var transition = movementTransition.Evaluate(
                standingCell,
                GetCurrentLayer(),
                direction,
                GetEffectiveSurfaceWorldY(),
                ResolveStandingDiceForMovement(),
                standingTier,
                ignoreStepHeight: true,
                isJumping: true);

            return IsSameTierJumpParallelRoll(transition);
        }

        bool IsSameTierJumpParallelRoll(MovementTransition transition) {
            if (currentDice == null || transition.TargetDice != currentDice) {
                return false;
            }

            var tier = currentDice.CurrentState.Tier;
            if (standingTier != tier) {
                return false;
            }

            var expectedLayer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            return standingSurfaceLayer == expectedLayer && transition.TargetLayer == expectedLayer;
        }

        void UpdateStandingAfterJumpDiceMove(Vector2Int toCell, DiceStackTier tier) {
            standingGridCell = toCell;
            standingTier = tier;
            standingSurfaceLayer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            ResubscribeStandingDice(currentDice);
        }

        bool TryExecuteJumpTopFallRoll(Direction direction, Vector2 nextXZ, float edgeLimit) {
            if (jumpPhase == JumpPhase.None || jumpDiceGridMoved) {
                return false;
            }

            SyncStandingDiceCache();
            if (currentDice?.View.DiceTransform == null || currentDice.IsDissolving) {
                return false;
            }

            if (standingTier != DiceStackTier.Top
                || currentDice.CurrentState.Tier != DiceStackTier.Top) {
                return false;
            }

            var targetPos = standingGridCell + direction.ToGridDelta();
            if (!registry.CanPlaceBottomDiceAt(targetPos)) {
                return false;
            }

            if (!currentDice.TryJumpRollThenDemote(direction, jumpYOffset)) {
                return false;
            }

            jumpDiceGridMoved = true;
            jumpDiceMoveKind = JumpDiceMoveKind.DemoteToBottom;
            QueuePendingJumpDiceStandingUpdate(currentDice.CurrentState.GridPos, DiceStackTier.Bottom);

            var diceCenter = currentDice.View.DiceTransform.position;
            var diceCenterAnchor = diceCenter;
            var nextOffset = WorldOffsetFromDiceCenter(diceCenter, nextXZ);
            var clamped = ClampToFace(nextOffset, edgeLimit);
            ApplyWorldPosition(new Vector3(diceCenter.x + clamped.x, 0f, diceCenter.z + clamped.y));
            BeginRollTracking(characterTransform.position, diceCenterAnchor);
            return true;
        }

        void SetStandingOnFloor(Vector2Int gridCell) {
            EndRollTracking();
            UnsubscribeStandingDice();
            currentDice = null;
            standingSurfaceLayer = SurfaceLayer.Floor;
            standingTier = DiceStackTier.Bottom;
            standingGridCell = gridCell;
        }

        void SetStandingOnDice(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            EndRollTracking();
            standingGridCell = gridCell;
            standingTier = tier;
            standingSurfaceLayer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            ResubscribeStandingDice(dice);
        }

        SurfaceLayer GetCurrentLayer() {
            return standingSurfaceLayer;
        }

        float GetWalkHalfExtent() {
            return board.CellSize * 0.5f;
        }

        Vector2 GetCellCenterXZ(Vector2Int grid) {
            var world = board.GridToWorld(grid);
            return new Vector2(world.x, world.z);
        }

        bool TryExecuteGridRoll(Direction direction, Vector2 nextXZ, float edgeLimit) {
            if (jumpPhase != JumpPhase.None) {
                return false;
            }

            SyncStandingDiceCache();
            if (currentDice?.View.DiceTransform == null || currentDice.IsDissolving) {
                return false;
            }

            if (standingTier != currentDice.CurrentState.Tier) {
                return false;
            }

            if (standingTier == DiceStackTier.Bottom && registry.HasTopAt(standingGridCell)) {
                return false;
            }

            var hasTopOnSameCell = registry.HasTopAt(standingGridCell);
            if (!RollResolver.TryRoll(
                currentDice.CurrentState,
                direction,
                registry,
                hasTopOnSameCell,
                out _)) {
                return false;
            }

            var diceCenter = currentDice.View.DiceTransform.position;
            var nextOffset = WorldOffsetFromDiceCenter(diceCenter, nextXZ);
            var clamped = ClampToFace(nextOffset, edgeLimit);
            ApplyWorldPosition(new Vector3(diceCenter.x + clamped.x, 0f, diceCenter.z + clamped.y));

            var characterAnchor = characterTransform.position;
            var diceCenterAnchor = diceCenter;

            if (!currentDice.TryRoll(direction)) {
                return false;
            }

            standingGridCell = currentDice.CurrentState.GridPos;
            BeginRollTracking(characterAnchor, diceCenterAnchor);
            return true;
        }

        bool TryExecuteTopFallRoll(Direction direction, Vector2 nextXZ, float edgeLimit) {
            if (jumpPhase != JumpPhase.None) {
                return false;
            }

            SyncStandingDiceCache();
            if (currentDice?.View.DiceTransform == null || currentDice.IsDissolving) {
                return false;
            }

            if (standingTier != DiceStackTier.Top
                || currentDice.CurrentState.Tier != DiceStackTier.Top) {
                return false;
            }

            var targetPos = standingGridCell + direction.ToGridDelta();
            if (!registry.CanPlaceBottomDiceAt(targetPos)) {
                return false;
            }

            var diceCenter = currentDice.View.DiceTransform.position;
            var nextOffset = WorldOffsetFromDiceCenter(diceCenter, nextXZ);
            var clamped = ClampToFace(nextOffset, edgeLimit);
            ApplyWorldPosition(new Vector3(diceCenter.x + clamped.x, 0f, diceCenter.z + clamped.y));

            var characterAnchor = characterTransform.position;
            var diceCenterAnchor = diceCenter;

            if (!currentDice.TryRollThenDemote(direction)) {
                return false;
            }

            standingGridCell = currentDice.CurrentState.GridPos;
            standingTier = DiceStackTier.Bottom;
            standingSurfaceLayer = SurfaceLayer.Bottom;
            BeginRollTracking(characterAnchor, diceCenterAnchor);
            return true;
        }

        static Vector2 CancelMoveIntoDirection(Vector2 current, Vector2 proposed, Direction direction) {
            var result = proposed;

            switch (direction) {
                case Direction.East:
                    if (proposed.x > current.x) {
                        result.x = current.x;
                    }

                    break;
                case Direction.West:
                    if (proposed.x < current.x) {
                        result.x = current.x;
                    }

                    break;
                case Direction.North:
                    if (proposed.y > current.y) {
                        result.y = current.y;
                    }

                    break;
                case Direction.South:
                    if (proposed.y < current.y) {
                        result.y = current.y;
                    }

                    break;
            }

            return result;
        }

        Vector2 ClampToCellInterior(Vector2 position, Vector2Int cell, float halfExtent) {
            var center = GetCellCenterXZ(cell);
            return new Vector2(
                Mathf.Clamp(position.x, center.x - halfExtent, center.x + halfExtent),
                Mathf.Clamp(position.y, center.y - halfExtent, center.y + halfExtent));
        }

        Vector2 ClampToBoardBounds(Vector2 position) {
            var clamped = ClampToWalkBounds(new Vector3(position.x, 0f, position.y));
            return new Vector2(clamped.x, clamped.z);
        }

        void UpdateDuringRoll(Vector2 input) {
            if (!isTrackingDiceRoll) {
                BeginRollTracking();
            }

            currentSpeed = 0f;
        }

        void OnDiceStateChanged(DiceState state) {
            if (!isInitialized) {
                return;
            }

            currentSpeed = 0f;
            if (standingSurfaceLayer != SurfaceLayer.Floor) {
                standingGridCell = state.GridPos;
            }

            if (TryGetStandingDice(out var standingDice)
                && standingDice == currentDice
                && state.GridPos == standingGridCell
                && state.Tier != standingTier) {
                SetStandingOnDice(state.GridPos, state.Tier, standingDice);
                SnapYToSurface();
            }
        }

        void ResubscribeStandingDice(DiceController dice) {
            UnsubscribeStandingDice();
            currentDice = dice;
            if (currentDice != null) {
                currentDice.StateChanged += OnDiceStateChanged;
            }
        }

        void UnsubscribeStandingDice() {
            if (currentDice != null) {
                currentDice.StateChanged -= OnDiceStateChanged;
            }
        }

        void MoveToFloorAtCurrentWorldPosition() {
            EndRollTracking();
            var gridCell = characterTransform != null
                ? board.WorldToGrid(characterTransform.position)
                : standingGridCell;
            SetStandingOnFloor(gridCell);
            SnapYToSurface();
        }

        void EnsureCharacterInstance() {
            characterMount = transform;

            if (characterTransform != null) {
                return;
            }

            if (characterObject != null) {
                var instance = Instantiate(characterObject, characterMount);
                instance.name = "CharacterVisual";
                characterTransform = instance.transform;
                return;
            }

            characterTransform = characterMount;
        }

        void EnsureCharacterPushCollider() {
            if (characterPushCollider != null || characterTransform == null) {
                return;
            }

            characterPushCollider = characterTransform.GetComponent<CapsuleCollider>();
            if (characterPushCollider == null) {
                Debug.LogWarning("CharacterController: CapsuleCollider is not assigned on the character prefab.");
                characterPushCollider = characterTransform.gameObject.AddComponent<CapsuleCollider>();
                characterPushCollider.isTrigger = true;
            }
        }

        float GetPushHorizontalRadius() {
            if (characterPushCollider == null) {
                return 0f;
            }

            var bounds = characterPushCollider.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        void GetPushWorldVerticalRange(out float bottomY, out float topY) {
            if (characterPushCollider == null) {
                bottomY = 0f;
                topY = 0f;
                return;
            }

            var bounds = characterPushCollider.bounds;
            bottomY = bounds.min.y;
            topY = bounds.max.y;
        }

        Vector2 GetWorldXZ() {
            if (characterTransform == null) {
                return Vector2.zero;
            }

            var position = characterTransform.position;
            return new Vector2(position.x, position.z);
        }

        float GetSurfaceWorldY() {
            if (IsOnFloor) {
                return board.FloorSurfaceWorldY;
            }

            if (TryGetStandingDice(out var standingDice)) {
                if (standingTier == DiceStackTier.Top
                    && standingDice.CurrentState.Tier == DiceStackTier.Bottom
                    && !registry.HasTopAt(standingGridCell)) {
                    return movementTransition.GetStackTopStandingSurfaceY(standingDice);
                }

                return standingDice.GetTopSurfaceWorldY();
            }

            return board.FloorSurfaceWorldY;
        }

        float GetEffectiveSurfaceWorldY() {
            var surfaceY = GetSurfaceWorldY();
            if (ShouldApplyJumpYOffsetToCharacter()) {
                surfaceY += jumpYOffset;
            }

            return surfaceY;
        }

        float GetCharacterWorldY() {
            var y = GetSurfaceWorldY() + movementSettings.CharacterHeightOffset;
            if (ShouldApplyJumpYOffsetToCharacter()) {
                y += jumpYOffset;
            }

            return y;
        }

        bool ShouldApplyJumpYOffsetToCharacter() {
            if (jumpPhase == JumpPhase.None) {
                return false;
            }

            if (IsOnFloor) {
                return true;
            }

            return TryGetStandingDice(out var standingDice) && standingDice.IsDissolving;
        }

        void ApplyWorldPosition(Vector3 worldPos) {
            if (characterTransform == null || board == null) {
                return;
            }

            worldPos.y = GetCharacterWorldY();
            characterTransform.position = worldPos;
            characterTransform.rotation = Quaternion.identity;
        }

        void SnapYToSurface() {
            if (characterTransform == null || isTrackingDiceRoll) {
                return;
            }

            var position = characterTransform.position;
            position.y = GetCharacterWorldY();
            characterTransform.position = position;
            characterTransform.rotation = Quaternion.identity;
        }

        Vector3 ClampToWalkBounds(Vector3 worldPos) {
            if (IsOnFloor) {
                var minX = 0f;
                var minZ = 0f;
                var maxX = (board.Width - 1) * board.CellSize;
                var maxZ = (board.Height - 1) * board.CellSize;
                worldPos.x = Mathf.Clamp(worldPos.x, minX, maxX);
                worldPos.z = Mathf.Clamp(worldPos.z, minZ, maxZ);
                return worldPos;
            }

            var center = GetCellCenterXZ(standingGridCell);
            var limit = GetWalkHalfExtent();
            worldPos.x = Mathf.Clamp(worldPos.x, center.x - limit, center.x + limit);
            worldPos.z = Mathf.Clamp(worldPos.z, center.y - limit, center.y + limit);
            return worldPos;
        }

        static Vector2 GetOffsetFromDiceCenter(DiceController dice, Vector3 worldPos) {
            if (dice?.View.DiceTransform == null) {
                return Vector2.zero;
            }

            var center = dice.View.DiceTransform.position;
            return new Vector2(worldPos.x - center.x, worldPos.z - center.z);
        }

        void BeginRollTracking() {
            if (characterTransform == null || currentDice?.View.DiceTransform == null) {
                return;
            }

            rollStartCharacterPosition = characterTransform.position;
            rollStartDiceCenter = currentDice.View.DiceTransform.position;
            isTrackingDiceRoll = true;
        }

        void BeginRollTracking(Vector3 characterAnchor, Vector3 diceCenterAnchor) {
            rollStartCharacterPosition = characterAnchor;
            rollStartDiceCenter = diceCenterAnchor;
            isTrackingDiceRoll = true;
        }

        void EndRollTracking() {
            if (!isTrackingDiceRoll) {
                return;
            }

            SyncPositionDuringRoll();
            isTrackingDiceRoll = false;
            SnapYToSurface();
            if (characterTransform != null) {
                characterTransform.rotation = Quaternion.identity;
            }
        }

        void SyncPositionDuringRoll() {
            if (!isTrackingDiceRoll || characterTransform == null || currentDice?.View.DiceTransform == null) {
                return;
            }

            var diceCenter = currentDice.View.DiceTransform.position;
            var delta = diceCenter - rollStartDiceCenter;
            var worldPosition = rollStartCharacterPosition + delta;
            worldPosition.y = currentDice.GetTopSurfaceWorldY() + movementSettings.CharacterHeightOffset;

            characterTransform.position = worldPosition;
            characterTransform.rotation = Quaternion.identity;
        }

        float GetDiceJumpHeight() {
            return board != null
                ? board.CellSize * physicsSettings.JumpHeightDiceMultiplier
                : physicsSettings.JumpHeightFallback;
        }

        float GetJumpHeightDiceRatio() {
            if (board == null || board.CellSize <= 0f) {
                return 0f;
            }

            return jumpYOffset / board.CellSize;
        }

        bool CanAttemptJumpGridMove() {
            return jumpPhase != JumpPhase.None && !jumpDiceGridMoved && board != null;
        }

        bool CanPerformJumpStackMove() {
            if (!CanAttemptJumpGridMove()) {
                return false;
            }

            var ratio = GetJumpHeightDiceRatio();
            return ratio >= physicsSettings.JumpHeightDiceMinMultiplier
                && ratio <= physicsSettings.JumpHeightDiceMultiplier + JumpHeightEpsilon;
        }

        static bool IsJumpStackWalkableTransition(
            MovementTransition transition,
            DiceStackTier standingTier) {
            return transition.Kind == MovementTransitionKind.Walkable
                && standingTier == DiceStackTier.Bottom
                && transition.TargetLayer == SurfaceLayer.Top;
        }

        void ResetPushState() {
            pushContactTime = 0f;
            pushTargetDice = null;
            hasPushDirection = false;
        }

        void UpdatePushContact(Vector2 input) {
            if (liftPhase != LiftPhase.None || jumpPhase != JumpPhase.None) {
                return;
            }

            if (registry == null || registry.AnyRolling() || registry.AnyCarried()) {
                ResetPushState();
                return;
            }

            CollectPushCandidates(input, pushCandidates);
            if (pushCandidates.Count == 0) {
                LogPushDebugWhenInput(input, "no-candidates", "stage=candidates count=0 (see overlap/canPush/direction logs)");
                ResetPushState();
                return;
            }

            var best = pushCandidates[0];
            var targetChanged = pushTargetDice != best.Dice || !hasPushDirection || pushDirection != best.Direction;
            if (targetChanged) {
                pushTargetDice = best.Dice;
                pushDirection = best.Direction;
                hasPushDirection = true;
                pushContactTime = 0f;
                LogPushDebug(
                    "target-selected",
                    $"stage=target dice={FormatMovementDice(best.Dice)} dir={best.Direction} " +
                    $"alignment={best.InputAlignment:F2} faceDistance={best.FaceDistance:F3}");
            }

            pushContactTime += Time.deltaTime;
            if (pushContactTime < movementSettings.PushHoldDuration) {
                LogPushDebug(
                    "hold-wait",
                    $"stage=hold dice={FormatMovementDice(best.Dice)} dir={best.Direction} " +
                    $"elapsed={pushContactTime:F2}/{movementSettings.PushHoldDuration:F2}");
                return;
            }

            var pushed = false;
            foreach (var candidate in pushCandidates) {
                if (candidate.Dice.TrySlide(candidate.Direction)) {
                    LogPushDebug(
                        "slide-ok",
                        $"stage=slide dice={FormatMovementDice(candidate.Dice)} dir={candidate.Direction}");
                    BeginPushFollow(candidate.Dice, candidate.Direction);
                    pushed = true;
                    break;
                }

                LogPushDebug(
                    $"slide-fail-{FormatMovementDice(candidate.Dice)}-{candidate.Direction}",
                    $"stage=slide dice={FormatMovementDice(candidate.Dice)} dir={candidate.Direction} TrySlide=false");
            }

            if (!pushed) {
                LogPushDebug("slide-all-failed", "stage=slide all candidates failed TrySlide");
            }

            ResetPushState();
        }

        void BeginPushFollow(DiceController dice, Direction direction) {
            EndPushFollow();
            pushFollowDice = dice;
            pushFollowDirection = direction;
            isPushFollowing = true;
            currentSpeed = 0f;
            pushFollowDice.StateChanged += OnPushFollowDiceStateChanged;
            UpdatePushFollowPosition();
        }

        void EndPushFollow() {
            if (pushFollowDice != null) {
                pushFollowDice.StateChanged -= OnPushFollowDiceStateChanged;
            }

            if (isPushFollowing && pushFollowDice != null) {
                UpdatePushFollowPosition();
            }

            isPushFollowing = false;
            pushFollowDice = null;
        }

        void OnPushFollowDiceStateChanged(DiceState state) {
            if (!isPushFollowing) {
                return;
            }

            EndPushFollow();
        }

        void UpdatePushFollowPosition() {
            if (pushFollowDice == null || board == null || characterTransform == null) {
                return;
            }

            SyncPositionToPushingDice();
        }

        void SyncPositionToPushingDice() {
            if (pushFollowDice == null || board == null || characterTransform == null) {
                return;
            }

            var diceTransform = pushFollowDice.View.DiceTransform;
            if (diceTransform == null) {
                return;
            }

            EndRollTracking();
            SyncPushFollowStanding();

            var diceCenter = diceTransform.position;
            var half = board.CellSize * 0.5f;
            var contactOffset = half + GetPushHorizontalRadius();
            var beforePosition = characterTransform.position;
            var position = pushFollowDirection switch {
                Direction.East => new Vector3(diceCenter.x - contactOffset, beforePosition.y, beforePosition.z),
                Direction.West => new Vector3(diceCenter.x + contactOffset, beforePosition.y, beforePosition.z),
                Direction.North => new Vector3(beforePosition.x, beforePosition.y, diceCenter.z - contactOffset),
                Direction.South => new Vector3(beforePosition.x, beforePosition.y, diceCenter.z + contactOffset),
                _ => beforePosition
            };

            ApplyWorldPosition(position);
        }

        void SyncPushFollowStanding() {
            if (pushFollowDice == null || board == null) {
                return;
            }

            var contactCell = pushFollowDice.CurrentState.GridPos
                + pushFollowDirection.Opposite().ToGridDelta();
            if (!board.IsInside(contactCell)) {
                return;
            }

            ResolveStandingAtGridCell(contactCell);
        }

        void ResolveStandingAtGridCell(Vector2Int gridCell) {
            if (registry == null || board == null || !board.IsInside(gridCell)) {
                return;
            }

            if (registry.CanPlaceBottomDiceAt(gridCell)) {
                ApplyFloorStanding(gridCell);
                return;
            }

            if (registry.TryGetBottomAt(gridCell, out var bottom)) {
                ApplyDiceStanding(gridCell, DiceStackTier.Bottom, bottom);
                return;
            }

            ApplyFloorStanding(gridCell);
        }

        void ApplyFloorStanding(Vector2Int gridCell) {
            if (standingSurfaceLayer == SurfaceLayer.Floor
                && standingGridCell == gridCell
                && currentDice == null) {
                return;
            }

            SetStandingOnFloor(gridCell);
        }

        void ApplyDiceStanding(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            var layer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            if (standingSurfaceLayer == layer
                && standingGridCell == gridCell
                && standingTier == tier
                && currentDice == dice) {
                return;
            }

            SetStandingOnDice(gridCell, tier, dice);
        }

        void CollectPushCandidates(Vector2 input, List<PushContactCandidate> candidates) {
            candidates.Clear();

            if (characterPushCollider == null) {
                LogPushDebugWhenInput(input, "no-collider", "stage=overlap characterPushCollider=null");
                return;
            }

            var bounds = characterPushCollider.bounds;
            var halfHeight = characterPushCollider.height * 0.5f - characterPushCollider.radius;
            var bottom = bounds.center - Vector3.up * halfHeight;
            var top = bounds.center + Vector3.up * halfHeight;
            var hits = Physics.OverlapCapsule(
                bottom,
                top,
                characterPushCollider.radius,
                ~0,
                QueryTriggerInteraction.Collide);

            var characterXZ = GetWorldXZ();
            var overlapSummary = new System.Text.StringBuilder();

            foreach (var hit in hits) {
                if (hit == characterPushCollider) {
                    continue;
                }

                var pushBody = hit.GetComponent<DicePushBody>();
                if (pushBody == null || pushBody.Dice == null || pushBody.Collider == null) {
                    overlapSummary.Append($" [{hit.name}:noPushBody]");
                    continue;
                }

                var diceLabel = FormatMovementDice(pushBody.Dice);
                if (pushBody.Dice.IsDissolving || pushBody.Dice.IsBusy) {
                    overlapSummary.Append($" [{diceLabel}:busy]");
                    continue;
                }

                if (!CanPushDice(pushBody.Dice, out var rejectReason)) {
                    overlapSummary.Append($" [{diceLabel}:canPush={rejectReason}]");
                    continue;
                }

                overlapSummary.Append($" [{diceLabel}:canPush=ok");
                foreach (Direction direction in new[] {
                    Direction.East, Direction.West, Direction.North, Direction.South }) {
                    if (TryEvaluatePushCandidate(
                        pushBody.Collider.bounds,
                        characterXZ,
                        input,
                        direction,
                        movementSettings.PushInputAlignment,
                        out _,
                        out _,
                        out var directionRejectReason)) {
                        overlapSummary.Append($" {direction}=ok");
                        continue;
                    }

                    overlapSummary.Append($" {direction}={directionRejectReason}");
                }

                overlapSummary.Append(']');

                var pushBounds = pushBody.Collider.bounds;
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.East);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.West);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.North);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.South);
            }

            candidates.Sort(ComparePushCandidates);

            LogPushDebugWhenInput(
                input,
                "overlap-summary",
                $"stage=overlap standing={FormatMovementGrid(GetStandingCell())} layer={standingSurfaceLayer} " +
                $"tier={standingTier} charXZ=({characterXZ.x:F2},{characterXZ.y:F2}) input=({input.x:F2},{input.y:F2}) " +
                $"hits={hits.Length} dice={overlapSummary} candidates={candidates.Count}");
        }

        void TryAddPushCandidate(
            List<PushContactCandidate> candidates,
            DiceController dice,
            Bounds bounds,
            Vector2 input,
            Vector2 characterPosition,
            Direction direction) {
            if (!TryEvaluatePushCandidate(
                bounds,
                characterPosition,
                input,
                direction,
                movementSettings.PushInputAlignment,
                out var inputAlignment,
                out var faceDistance,
                out _)) {
                return;
            }

            candidates.Add(new PushContactCandidate {
                Dice = dice,
                Direction = direction,
                InputAlignment = inputAlignment,
                FaceDistance = faceDistance
            });
        }

        static int ComparePushCandidates(PushContactCandidate a, PushContactCandidate b) {
            var alignmentCompare = b.InputAlignment.CompareTo(a.InputAlignment);
            if (alignmentCompare != 0) {
                return alignmentCompare;
            }

            return a.FaceDistance.CompareTo(b.FaceDistance);
        }

        static bool TryEvaluatePushCandidate(
            Bounds bounds,
            Vector2 characterPosition,
            Vector2 input,
            Direction direction,
            float minInputAlignment,
            out float inputAlignment,
            out float faceDistance,
            out string rejectReason) {
            rejectReason = null;
            inputAlignment = Vector2.Dot(input, GetDirectionInputVector(direction));
            if (inputAlignment < minInputAlignment) {
                faceDistance = 0f;
                rejectReason = $"input={inputAlignment:F2}<{minInputAlignment:F2}";
                return false;
            }

            var charX = characterPosition.x;
            var charZ = characterPosition.y;

            switch (direction) {
                case Direction.East:
                    if (charX > bounds.center.x + EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charX={charX:F3}>centerX={bounds.center.x:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.min.x);
                    break;
                case Direction.West:
                    if (charX < bounds.center.x - EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charX={charX:F3}<centerX={bounds.center.x:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.max.x);
                    break;
                case Direction.North:
                    if (charZ > bounds.center.z + EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charZ={charZ:F3}>centerZ={bounds.center.z:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.min.z);
                    break;
                case Direction.South:
                    if (charZ < bounds.center.z - EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charZ={charZ:F3}<centerZ={bounds.center.z:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.max.z);
                    break;
                default:
                    faceDistance = 0f;
                    rejectReason = "invalidDirection";
                    return false;
            }

            return true;
        }

        static Vector2 GetDirectionInputVector(Direction direction) {
            return direction switch {
                Direction.East => Vector2.right,
                Direction.West => Vector2.left,
                Direction.North => Vector2.up,
                Direction.South => Vector2.down,
                _ => Vector2.zero
            };
        }

        bool CanPushDice(DiceController dice) {
            return CanPushDice(dice, out _);
        }

        bool CanPushDice(DiceController dice, out string rejectReason) {
            rejectReason = null;
            if (dice == null || registry == null) {
                rejectReason = dice == null ? "nullDice" : "nullRegistry";
                return false;
            }

            if (TryGetStandingDice(out var standingDice) && dice == standingDice) {
                rejectReason = "standingDice";
                return false;
            }

            if (!IsPushReachableFromStanding(dice)) {
                rejectReason = "notReachable";
                return false;
            }

            if (IsOnFloor) {
                if (dice.CurrentState.Tier != DiceStackTier.Bottom) {
                    rejectReason = "floorRequiresBottom";
                    return false;
                }

                if (registry.HasTopAt(dice.CurrentState.GridPos)) {
                    rejectReason = "floorRequiresNoTop";
                    return false;
                }

                return true;
            }

            if (dice.CurrentState.Tier != DiceStackTier.Top) {
                rejectReason = "onDiceRequiresTop";
                return false;
            }

            return true;
        }

        bool CanLiftDice(DiceController dice) {
            if (dice == null || registry == null) {
                return false;
            }

            if (TryGetStandingDice(out var standingDice) && dice == standingDice) {
                return false;
            }

            if (!IsLiftReachableFromStanding(dice)) {
                return false;
            }

            if (IsOnFloor) {
                if (dice.CurrentState.Tier == DiceStackTier.Top) {
                    return true;
                }

                return dice.CurrentState.Tier == DiceStackTier.Bottom
                    && !registry.HasTopAt(dice.CurrentState.GridPos);
            }

            if (standingTier == DiceStackTier.Bottom) {
                return true;
            }

            return dice.CurrentState.Tier == DiceStackTier.Top;
        }

        DiceSlot GetPlayerDiceSlot() {
            var tier = IsOnFloor ? DiceStackTier.Bottom : standingTier;
            return new DiceSlot(GetStandingCell(), tier);
        }

        bool IsPushReachableFromStanding(DiceController dice) {
            if (dice == null) {
                return false;
            }

            return DiceStackAdjacency.IsAdjacentForPush(
                GetPlayerDiceSlot(),
                DiceSlot.FromDice(dice),
                IsOnFloor);
        }

        bool IsLiftReachableFromStanding(DiceController dice) {
            if (dice == null) {
                return false;
            }

            return DiceStackAdjacency.IsAdjacentForLift(
                GetPlayerDiceSlot(),
                DiceSlot.FromDice(dice));
        }

        void LogPushDebugWhenInput(Vector2 input, string key, string message) {
            if (input.sqrMagnitude <= 0.01f) {
                return;
            }

            LogPushDebug(key, message);
        }

        void LogPushDebug(string key, string message) {
            if (!movementSettings.DebugPush) {
                return;
            }

            var now = Time.time;
            if (key == debugLastPushKey && now - debugLastPushLogTime < PushDebugLogInterval) {
                return;
            }

            debugLastPushKey = key;
            debugLastPushLogTime = now;
            Debug.Log($"[PushDebug] {message}");
        }

        static Vector2 WorldOffsetFromDiceCenter(Vector3 diceCenter, Vector2 worldPosition) {
            return new Vector2(worldPosition.x - diceCenter.x, worldPosition.y - diceCenter.z);
        }

        static bool TryGetPrimaryDirection(Vector2 move, out Direction direction) {
            direction = default;
            if (move.sqrMagnitude <= 0f) {
                return false;
            }

            if (Mathf.Abs(move.x) >= Mathf.Abs(move.y)) {
                direction = move.x > 0f ? Direction.East : Direction.West;
            } else {
                direction = move.y > 0f ? Direction.North : Direction.South;
            }

            return true;
        }

        static Vector2 ClampToFace(Vector2 offset, float edgeLimit) {
            return new Vector2(
                Mathf.Clamp(offset.x, -edgeLimit, edgeLimit),
                Mathf.Clamp(offset.y, -edgeLimit, edgeLimit));
        }

        static Vector2 GetInputDirection() {
            var input = Vector2.zero;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) {
                input.x += 1f;
            }

            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) {
                input.x -= 1f;
            }

            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) {
                input.y += 1f;
            }

            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) {
                input.y -= 1f;
            }

            return input;
        }

        void UpdateLastFacing(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                return;
            }

            if (TryInputToDirection(input, out var direction)) {
                lastFacing = direction;
                hasLastFacing = true;
            }
        }

        static bool TryInputToDirection(Vector2 input, out Direction direction) {
            direction = default;
            if (input.sqrMagnitude <= 0f) {
                return false;
            }

            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y)) {
                direction = input.x > 0f ? Direction.East : Direction.West;
            } else {
                direction = input.y > 0f ? Direction.North : Direction.South;
            }

            return true;
        }

        static bool TryGetDirectionKeyDown(out Direction direction) {
            direction = default;

            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) {
                direction = Direction.East;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) {
                direction = Direction.West;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) {
                direction = Direction.North;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) {
                direction = Direction.South;
                return true;
            }

            return false;
        }

        void SnapToStandingCellCenter() {
            if (characterTransform == null || board == null) {
                return;
            }

            var center = GetCellCenterXZ(GetStandingCell());
            ApplyWorldPosition(new Vector3(center.x, 0f, center.y));
        }

        Vector3 GetCarryWorldPosition() {
            if (characterTransform == null) {
                return Vector3.zero;
            }

            var position = characterTransform.position;
            return new Vector3(position.x, position.y + movementSettings.CarryVerticalOffset, position.z);
        }

        bool TryBeginLift() {
            if (liftPhase != LiftPhase.None || isPushFollowing || jumpPhase != JumpPhase.None) {
                return false;
            }

            if (registry == null || registry.AnyRolling() || registry.AnyCarried()) {
                return false;
            }

            var input = GetInputDirection();
            if (input.sqrMagnitude > 0f) {
                UpdateLastFacing(input);
            }

            if (!hasLastFacing || !TryFindLiftTarget(out var targetDice)) {
                return false;
            }

            carriedDice = targetDice;
            liftPhase = LiftPhase.Lifting;
            ResetPushState();
            SnapToStandingCellCenter();

            if (!carriedDice.TryBeginCarry(GetCarryWorldPosition(), OnLiftComplete)) {
                carriedDice = null;
                liftPhase = LiftPhase.None;
                return false;
            }

            return true;
        }

        void OnLiftComplete() {
            if (liftPhase == LiftPhase.Lifting) {
                liftPhase = LiftPhase.Carrying;
            }
        }

        bool TryPlaceCarriedDice(Direction direction) {
            if (liftPhase != LiftPhase.Carrying || carriedDice == null || board == null) {
                return false;
            }

            var originGrid = GetStandingCell();
            var targetGrid = originGrid + direction.ToGridDelta();

            DiceStackTier targetTier;
            if (registry.CanPlaceBottomDiceAt(targetGrid)) {
                targetTier = DiceStackTier.Bottom;
            } else if (registry.CanPlaceTopDiceAt(targetGrid)) {
                targetTier = DiceStackTier.Top;
            } else {
                return false;
            }

            liftPhase = LiftPhase.Placing;
            var fromWorld = GetCarryWorldPosition();

            if (!carriedDice.TryPlaceAt(targetGrid, targetTier, fromWorld, OnPlaceComplete)) {
                liftPhase = LiftPhase.Carrying;
                return false;
            }

            return true;
        }

        void OnPlaceComplete() {
            carriedDice = null;
            liftPhase = LiftPhase.None;
        }

        void EndCarryState() {
            carriedDice = null;
            liftPhase = LiftPhase.None;
        }

        bool TryFindLiftTarget(out DiceController targetDice) {
            targetDice = null;

            if (registry == null || board == null || !hasLastFacing) {
                return false;
            }

            var neighborGrid = GetStandingCell() + lastFacing.ToGridDelta();
            if (!board.IsInside(neighborGrid)) {
                return false;
            }

            DiceController candidate = ResolveLiftCandidateAt(neighborGrid);
            if (candidate == null) {
                return false;
            }

            if (candidate == ResolveStandingDiceForMovement()
                || candidate.IsDissolving
                || candidate.IsBusy
                || !CanLiftDice(candidate)) {
                return false;
            }

            targetDice = candidate;
            return true;
        }

        DiceController ResolveLiftCandidateAt(Vector2Int neighborGrid) {
            registry.TryGetTopAt(neighborGrid, out var top);
            registry.TryGetBottomAt(neighborGrid, out var bottom);

            if (top != null && IsLiftReachableFromStanding(top)) {
                return top;
            }

            if (bottom != null && IsLiftReachableFromStanding(bottom)) {
                return bottom;
            }

            return null;
        }

        bool TryBeginJump() {
            if (jumpPhase != JumpPhase.None || liftPhase != LiftPhase.None || isPushFollowing) {
                return false;
            }

            if (!IsOnFloor && currentDice != null && currentDice.IsRolling) {
                return false;
            }

            if (registry != null && (registry.AnyRolling() || registry.AnyCarried())) {
                return false;
            }

            jumpMotion = GravityMotion.CreateLaunch(GetDiceJumpHeight(), physicsSettings.Gravity);
            jumpPhase = JumpPhase.Airborne;
            jumpYOffset = 0f;
            jumpDiceGridMoved = false;
            jumpArcRollActive = false;
            jumpDiceMoveKind = JumpDiceMoveKind.None;
            ClearPendingJumpDiceStandingUpdate();
            ResetPushState();
            return true;
        }

        void UpdateJump() {
            if (jumpPhase == JumpPhase.None) {
                return;
            }

            if (jumpDiceGridMoved && currentDice != null && currentDice.IsRolling && !jumpArcRollActive) {
                return;
            }

            jumpMotion = GravityMotion.Step(jumpMotion, physicsSettings.Gravity, Time.deltaTime);
            jumpYOffset = jumpMotion.Offset;

            if (jumpMotion.IsGrounded) {
                if (jumpArcRollActive && currentDice != null && currentDice.IsRolling) {
                    return;
                }

                EndJump();
            }
        }

        void ApplyStandingDiceJumpVisual() {
            if (jumpPhase == JumpPhase.None || board == null) {
                return;
            }

            if (currentDice != null && currentDice.IsRolling) {
                return;
            }

            DiceController targetDice = null;
            if (TryGetStandingDice(out var standingDice) && !standingDice.IsDissolving) {
                targetDice = standingDice;
            }

            if (jumpVisualDice != null && jumpVisualDice != targetDice) {
                ClearJumpVisualDice(jumpVisualDice);
            }

            jumpVisualDice = targetDice;
            if (targetDice == null) {
                return;
            }

            if (jumpDiceMoveKind == JumpDiceMoveKind.StackOntoTop) {
                targetDice.View.ClearVisualYOffset(board);
                return;
            }

            targetDice.View.ApplyVisualYOffset(board, jumpYOffset);
            if (standingTier == DiceStackTier.Bottom && registry.HasTopAt(standingGridCell)) {
                registry.SyncStackedTopAt(standingGridCell, board);
            }
        }

        void ClearJumpVisualDice(DiceController dice) {
            if (dice?.View == null || board == null) {
                return;
            }

            dice.View.ClearVisualYOffset(board);
            if (dice.CurrentState.Tier == DiceStackTier.Bottom
                && registry != null
                && registry.HasTopAt(dice.CurrentState.GridPos)) {
                registry.SyncStackedTopAt(dice.CurrentState.GridPos, board);
            }
        }

        void EndJump() {
            if (jumpVisualDice != null) {
                ClearJumpVisualDice(jumpVisualDice);
                jumpVisualDice = null;
            }

            jumpPhase = JumpPhase.None;
            jumpMotion = new VerticalMotionState {
                Offset = 0f,
                VelocityY = 0f,
                IsGrounded = true
            };
            jumpYOffset = 0f;
            jumpDiceGridMoved = false;
            jumpArcRollActive = false;
            jumpDiceMoveKind = JumpDiceMoveKind.None;
            ClearPendingJumpDiceStandingUpdate();
            SnapYToSurface();
        }
    }
}
