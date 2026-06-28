using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Character;
using DiceGame.Gameplay.Coupling;
using DiceGame.Grid;
using DiceGame.Placement;
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
        const float JumpParallelRollLogInterval = 0.25f;

        MovementTransitionEvaluator movementTransition;
        string debugLastMovementBlockKey;
        float debugLastMovementBlockLogTime = -1f;
        string debugLastPushKey;
        float debugLastPushLogTime = -1f;
        string debugLastJumpParallelRollKey;
        float debugLastJumpParallelRollLogTime = -1f;

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

        PlacementService placement;
        DiceRegistry registry;
        CharacterStandingController standingController;
        CharacterTransformDriver transformDriver;
        DiceCharacterCoupling coupling;
        CharacterMovePlanner movePlanner;
        CharacterMovementExecutor movementExecutor;
        DissolveDescentHoldState dissolveHoldState;
        Transform characterMount;
        Transform characterTransform;
        CapsuleCollider characterPushCollider;
        float currentSpeed;
        float pushContactTime;
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

        struct PushContactCandidate {
            public DiceController Dice;
            public Direction Direction;
            public float InputAlignment;
            public float FaceDistance;
        }

        public bool IsOnFloor => standingController.IsOnFloor;
        public bool IsBusy => !IsOnFloor && standingController.CurrentDice != null && standingController.CurrentDice.IsRolling;
        public bool IsCarrying => liftPhase != LiftPhase.None;
        public Vector2 FacePosition => standingController.TryGetStandingDice(out var standingDice)
            ? CharacterTransformDriver.GetOffsetFromDiceCenter(standingDice, characterTransform != null ? characterTransform.position : Vector3.zero)
            : Vector2.zero;
        public DiceController CurrentDice => standingController.CurrentDice;

        public void Configure(
            Board targetBoard,
            PlacementService targetPlacement,
            DiceController startDice,
            CharacterMovementSettings movement,
            PhysicsSettings physics) {
            board = targetBoard;
            placement = targetPlacement;
            registry = targetPlacement.Dice;
            movementSettings = movement;
            physicsSettings = physics;
            standingController = new CharacterStandingController();
            coupling = new DiceCharacterCoupling();
            standingController.Configure(placement, () => coupling.EndRollTracking());
            if (startDice != null) {
                standingController.SetInitialStanding(CharacterPlacement.OnDice(
                    startDice.CurrentState.GridPos,
                    startDice.CurrentState.Tier,
                    startDice));
            } else {
                standingController.SetInitialStanding(CharacterPlacement.OnFloor(startGridCellFromTransform()));
            }

            Initialize();
        }

        Vector2Int startGridCellFromTransform() {
            return characterTransform != null && board != null
                ? board.WorldToGrid(characterTransform.position)
                : Vector2Int.zero;
        }

        public void Initialize() {
            if (board == null || placement == null || registry == null) {
                Debug.LogError("CharacterController: Board or PlacementService is not assigned.");
                return;
            }

            if (movementSettings == null || physicsSettings == null) {
                Debug.LogError("CharacterController: Movement or physics settings are not assigned.");
                return;
            }

            if (standingController.CurrentDice != null) {
                standingController.CurrentDice.View.EnsureDiceInstance();
                if (standingController.CurrentDice.View.DiceTransform == null) {
                    Debug.LogError("CharacterController: Dice visual is not available.");
                    return;
                }
            }

            EnsureCharacterInstance();
            EnsureCharacterPushCollider();

            transformDriver = new CharacterTransformDriver();
            transformDriver.Configure(
                board,
                characterTransform,
                () => standingController.Current,
                GetCharacterWorldY,
                () => coupling.IsTrackingRoll);

            movementTransition = placement.Passability;
            movementTransition.SetJumpParallelRollDebugLog(
                movementSettings.DebugJumpParallelRoll ? LogJumpParallelRoll : null);

            coupling.Configure(
                board,
                registry,
                standingController,
                transformDriver,
                movementSettings,
                () => jumpYOffset,
                () => jumpMotion);

            movePlanner = new CharacterMovePlanner(
                board,
                movementTransition,
                transformDriver,
                LogJumpParallelRoll);
            movementExecutor = new CharacterMovementExecutor(
                board,
                movementSettings,
                standingController,
                transformDriver,
                coupling);
            dissolveHoldState = new DissolveDescentHoldState();
            standingController.StandingDiceStateChanged += OnStandingDiceStateChanged;

            currentSpeed = 0f;
            isInitialized = true;

            if (!IsOnFloor
                && standingController.TryGetStandingDice(out var startStanding)
                && startStanding.View.DiceTransform != null) {
                var center = startStanding.View.DiceTransform.position;
                transformDriver.ApplyWorldPosition(new Vector3(center.x, 0f, center.z));
            } else {
                transformDriver.SnapYToSurface();
            }
        }

        void OnStandingDiceStateChanged(DiceState state) {
            currentSpeed = 0f;
            if (!isInitialized) {
                return;
            }

            if (standingController.TryGetStandingDice(out var standingDice)
                && standingDice == standingController.CurrentDice
                && state.GridPos == standingController.GridCell
                && state.Tier != standingController.Tier) {
                transformDriver.SnapYToSurface();
            }
        }

        public void OnStandingDiceDissolved(DiceController dissolvedDice) {
            if (!isInitialized || !standingController.TryGetStandingDice(out var standingDice) || standingDice != dissolvedDice) {
                return;
            }

            var grid = standingController.GridCell;
            if (standingController.Tier == DiceStackTier.Top && registry.TryGetBottomAt(grid, out var bottom)) {
                standingController.SetOnDice(grid, DiceStackTier.Bottom, bottom);
                transformDriver.SnapYToSurface();
                return;
            }

            if (standingController.Tier == DiceStackTier.Bottom && registry.TryGetTopAt(grid, out var top)) {
                standingController.SetOnDice(grid, DiceStackTier.Top, top);
                transformDriver.SnapYToSurface();
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        public void OnStandingDiceBecameGhost(DiceController ghostDice) {
            if (!isInitialized || !standingController.TryGetStandingDice(out var standingDice) || standingDice != ghostDice) {
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        void OnDisable() {
            coupling?.EndRollTracking();
            EndCarryState();
            EndJump();
            standingController?.UnsubscribeAll();
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

            if (!IsOnFloor
                && standingController.CurrentDice != null
                && coupling.IsTrackingRoll
                && !standingController.CurrentDice.IsRolling) {
                var wasArcRoll = coupling.CompleteRollIfFinished(standingController.CurrentDice);
                if (wasArcRoll && jumpPhase != JumpPhase.None) {
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

            var isRolling = !IsOnFloor && standingController.CurrentDice != null && standingController.CurrentDice.IsRolling;

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

            if (!IsOnFloor && standingController.CurrentDice != null && standingController.CurrentDice.IsRolling) {
                coupling.SyncVisual();
            } else if (!IsOnFloor || jumpPhase != JumpPhase.None) {
                transformDriver.SnapYToSurface();
            }
        }

        void UpdateSurfaceMovement(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                currentSpeed = 0f;
                ResetPushState();
                dissolveHoldState.Reset();
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
            var currentXZ = transformDriver.GetWorldXZ();
            var standingCell = standingController.GridCell;
            var fromLayer = standingController.Layer;
            var fromSurfaceY = GetEffectiveSurfaceWorldY();
            var halfExtent = transformDriver.GetWalkHalfExtent();
            var nextXZ = currentXZ + move;

            if (IsOnFloor) {
                nextXZ = transformDriver.ClampToBoardBounds(nextXZ);
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

            transformDriver.ApplyWorldPosition(new Vector3(nextXZ.x, 0f, nextXZ.y));
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
            var isJumping = jumpPhase != JumpPhase.None;
            var hasJumpCapability = false;
            JumpCoupledMoveCapability jumpCapability = default;
            if (isJumping) {
                hasJumpCapability = TryGetJumpCoupledMoveCapability(out jumpCapability);
            }

            var plan = movePlanner.TryBuildPlan(
                currentXZ,
                move,
                standingCell,
                fromLayer,
                fromSurfaceY,
                halfExtent,
                standingController,
                isJumping,
                hasJumpCapability,
                jumpCapability);

            if (plan.Kind == CharacterMoveKind.Blocked) {
                LogPositionMovementBlock(
                    plan.Transition.Kind == MovementTransitionKind.BlockedStepOnly
                        ? "DissolveDescentBlocked"
                        : "TransitionBlocked",
                    plan.FromCell,
                    plan.ToCell,
                    fromLayer,
                    fromSurfaceY,
                    halfExtent,
                    currentXZ,
                    nextXZ,
                    move,
                    plan.Transition.Kind,
                    $"stack={FormatMovementStack(plan.ToCell)}");
            }

            if (movementExecutor.TryExecutePlan(
                plan,
                currentXZ,
                ref nextXZ,
                move,
                fromLayer,
                fromSurfaceY,
                halfExtent,
                isJumping,
                hasJumpCapability,
                jumpCapability,
                LogJumpParallelRoll,
                dissolveHoldState,
                out var consumedMovement)) {
                if (consumedMovement) {
                    UpdatePushContact(Vector2.zero);
                }

                return consumedMovement;
            }

            return false;
        }

        bool TryGetJumpCoupledMoveCapability(out JumpCoupledMoveCapability capability) {
            var evaluated = JumpCoupledMoveGate.TryEvaluate(
                jumpPhase != JumpPhase.None,
                coupling.JumpDiceGridMoved,
                physicsSettings,
                jumpMotion,
                GetDiceJumpHeight(),
                out capability);

            if (!evaluated || !capability.IsJumping) {
                return evaluated;
            }

            if (coupling.JumpDiceGridMoved) {
                LogJumpParallelRoll(
                    "JumpCoupledMoveGate blocked reason=already-moved " +
                    $"jumpPhase={jumpPhase} coupling.JumpDiceGridMoved={coupling.JumpDiceGridMoved}");
                return true;
            }

            if (!capability.AllowCrossCellMove) {
                LogJumpParallelRoll(
                    $"JumpCoupledMoveGate blocked timeline={capability.Timeline:F3} " +
                    $"oneCellMax={physicsSettings.JumpGridMoveOneCellMaxTimeline:F3}");
                return true;
            }

            LogJumpParallelRoll(
                $"JumpCoupledMoveGate allowed timeline={capability.Timeline:F3} " +
                $"maxDistance={capability.MaxDistance} allowTierChange={capability.AllowTierChange} " +
                $"twoCellMax={physicsSettings.JumpGridMoveTwoCellMaxTimeline:F3} " +
                $"oneCellMax={physicsSettings.JumpGridMoveOneCellMaxTimeline:F3}");
            return true;
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

            var standingDice = standingController.ResolveStandingDiceForMovement();
            var target = movementTransition.IsWalkableBetween(
                standingCell,
                nextCell,
                fromLayer,
                fromSurfaceY,
                standingDice,
                standingController.Tier)
                ? DescribeWalkableTarget(standingCell, nextCell, fromLayer, fromSurfaceY)
                : "(none)";

            var detail =
                $"from={FormatMovementGrid(standingCell)} to={FormatMovementGrid(nextCell)} " +
                $"posCell={FormatMovementGrid(board.WorldToGrid(new Vector3(nextXZ.x, 0f, nextXZ.y)))} " +
                $"layer={fromLayer} tier={standingController.Tier} dice={FormatMovementDice(standingDice)} " +
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

            var standingDice = standingController.ResolveStandingDiceForMovement();
            var transition = movementTransition.Evaluate(
                fromCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standingController.Tier);
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

        void LogJumpParallelRoll(string message) {
            if (!movementSettings.DebugJumpParallelRoll) {
                return;
            }

            if (message == debugLastJumpParallelRollKey
                && Time.time - debugLastJumpParallelRollLogTime < JumpParallelRollLogInterval) {
                return;
            }

            debugLastJumpParallelRollKey = message;
            debugLastJumpParallelRollLogTime = Time.time;
            Debug.Log($"[JumpParallelRoll] {message}");
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

        void UpdateDuringRoll(Vector2 input) {
            coupling.EnsureTrackingFromCurrentPose();
            currentSpeed = 0f;
        }

        void MoveToFloorAtCurrentWorldPosition() {
            coupling.EndRollTracking();
            var gridCell = characterTransform != null
                ? board.WorldToGrid(characterTransform.position)
                : standingController.GridCell;
            standingController.SetOnFloor(gridCell);
            transformDriver.SnapYToSurface();
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

        float GetSurfaceWorldY() {
            if (IsOnFloor) {
                return board.FloorSurfaceWorldY;
            }

            if (standingController.TryGetStandingDice(out var standingDice)) {
                if (standingController.Tier == DiceStackTier.Top
                    && standingDice.CurrentState.Tier == DiceStackTier.Bottom
                    && !registry.HasTopAt(standingController.GridCell)) {
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

            return standingController.TryGetStandingDice(out var standingDice) && standingDice.IsDissolving;
        }

        float GetDiceJumpHeight() {
            return board != null
                ? board.CellSize * physicsSettings.JumpHeightDiceMultiplier
                : physicsSettings.JumpHeightFallback;
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

            coupling.EndRollTracking();
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

            transformDriver.ApplyWorldPosition(position);
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
            if (standingController.Layer == SurfaceLayer.Floor
                && standingController.GridCell == gridCell
                && standingController.CurrentDice == null) {
                return;
            }

            standingController.SetOnFloor(gridCell);
        }

        void ApplyDiceStanding(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            var layer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            if (standingController.Layer == layer
                && standingController.GridCell == gridCell
                && standingController.Tier == tier
                && standingController.CurrentDice == dice) {
                return;
            }

            standingController.SetOnDice(gridCell, tier, dice);
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

            var characterXZ = transformDriver.GetWorldXZ();
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
                $"stage=overlap standing={FormatMovementGrid(standingController.GridCell)} layer={standingController.Layer} " +
                $"tier={standingController.Tier} charXZ=({characterXZ.x:F2},{characterXZ.y:F2}) input=({input.x:F2},{input.y:F2}) " +
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

            if (standingController.TryGetStandingDice(out var standingDice) && dice == standingDice) {
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

            if (standingController.TryGetStandingDice(out var standingDice) && dice == standingDice) {
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

            if (standingController.Tier == DiceStackTier.Bottom) {
                return true;
            }

            return dice.CurrentState.Tier == DiceStackTier.Top;
        }

        DiceSlot GetPlayerDiceSlot() {
            var tier = IsOnFloor ? DiceStackTier.Bottom : standingController.Tier;
            return new DiceSlot(standingController.GridCell, tier);
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

            var center = transformDriver.GetCellCenterXZ(standingController.GridCell);
            transformDriver.ApplyWorldPosition(new Vector3(center.x, 0f, center.y));
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

            var originGrid = standingController.GridCell;
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

            var neighborGrid = standingController.GridCell + lastFacing.ToGridDelta();
            if (!board.IsInside(neighborGrid)) {
                return false;
            }

            DiceController candidate = ResolveLiftCandidateAt(neighborGrid);
            if (candidate == null) {
                return false;
            }

            if (candidate == standingController.ResolveStandingDiceForMovement()
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

            if (!IsOnFloor && standingController.CurrentDice != null && standingController.CurrentDice.IsRolling) {
                return false;
            }

            if (registry != null && (registry.AnyRolling() || registry.AnyCarried())) {
                return false;
            }

            jumpMotion = GravityMotion.CreateLaunch(GetDiceJumpHeight(), physicsSettings.Gravity);
            jumpPhase = JumpPhase.Airborne;
            jumpYOffset = 0f;
            coupling.ResetJumpSessionFlags();
            ResetPushState();
            return true;
        }

        void UpdateJump() {
            if (jumpPhase == JumpPhase.None) {
                return;
            }

            if (coupling.JumpDiceGridMoved && standingController.CurrentDice != null && standingController.CurrentDice.IsRolling && !coupling.IsJumpArc) {
                return;
            }

            jumpMotion = GravityMotion.Step(jumpMotion, physicsSettings.Gravity, Time.deltaTime);
            jumpYOffset = jumpMotion.Offset;

            if (jumpMotion.IsGrounded) {
                if (coupling.IsJumpArc && standingController.CurrentDice != null && standingController.CurrentDice.IsRolling) {
                    return;
                }

                EndJump();
            }
        }

        void ApplyStandingDiceJumpVisual() {
            if (jumpPhase == JumpPhase.None || board == null) {
                return;
            }

            if (standingController.CurrentDice != null && standingController.CurrentDice.IsRolling) {
                return;
            }

            DiceController targetDice = null;
            if (standingController.TryGetStandingDice(out var standingDice) && !standingDice.IsDissolving) {
                targetDice = standingDice;
            }

            if (jumpVisualDice != null && jumpVisualDice != targetDice) {
                ClearJumpVisualDice(jumpVisualDice);
            }

            jumpVisualDice = targetDice;
            if (targetDice == null) {
                return;
            }

            if (coupling.JumpMoveKind == JumpDiceMoveKind.StackOntoTop) {
                targetDice.View.ClearVisualYOffset(board);
                return;
            }

            targetDice.View.ApplyVisualYOffset(board, jumpYOffset);
            if (standingController.Tier == DiceStackTier.Bottom && registry.HasTopAt(standingController.GridCell)) {
                registry.SyncStackedTopAt(standingController.GridCell, board);
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
            coupling.ResetJumpSessionFlags();
            transformDriver.SnapYToSurface();
        }
    }
}
