using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Gameplay.Character;
using DiceGame.Gameplay.Coupling;
using DiceGame.Gameplay.Input;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Placement.Support;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class CharacterController : MonoBehaviour
    {
        const float EdgeEpsilon = 0.001f;

        [SerializeField] Board board;
        [SerializeField] GameObject characterObject;
        [SerializeField] CharacterInputReader inputReader;

        ICharacterInputSource inputSource;

        CharacterMovementSettings movementSettings;
        PhysicsSettings physicsSettings;

        const float MovementBlockLogInterval = 0.25f;
        const float PushDebugLogInterval = 0.25f;
        const float JumpParallelRollLogInterval = 0.25f;
        const float JumpLogInterval = 0.25f;
        const float HeightTransferLogInterval = 0.25f;

        MovementTransitionEvaluator movementTransition;
        string debugLastMovementBlockKey;
        float debugLastMovementBlockLogTime = -1f;
        string debugLastPushKey;
        float debugLastPushLogTime = -1f;
        string debugLastJumpParallelRollKey;
        float debugLastJumpParallelRollLogTime = -1f;
        string debugLastJumpKey;
        float debugLastJumpLogTime = -1f;
        string debugLastHeightTransferKey;
        float debugLastHeightTransferLogTime = -1f;

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
        PlayerMatchActionContext matchActionContext;
        GameFlowController crushOutcomeFlow;
        CharacterStandingController standingController;
        CharacterTransformDriver transformDriver;
        DiceCharacterCoupling coupling;
        CharacterMovePlanner movePlanner;
        CharacterMovementExecutor movementExecutor;
        DissolveDescentHoldState dissolveHoldState;
        PendingJumpLandingState pendingJumpLandingState;
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
        bool pushFollowLimitOneCell;
        Vector3 pushFollowDiceStartWorld;
        bool isInitialized;
        readonly List<PushContactCandidate> pushCandidates = new();
        LiftPhase liftPhase;
        DiceController carriedDice;
        Direction lastFacing;
        bool hasLastFacing;
        JumpPhase jumpPhase;
        bool pendingEndJump;
        bool isFalling;
        VerticalMotionState fallMotion;
        float fallGroundWorldY;
        Vector2Int fallGridCell;
        VerticalMotionState jumpMotion;
        float jumpYOffset;
        DiceController jumpVisualDice;
        bool hasJumpStartPlacement;
        Vector2Int jumpStartGridCell;
        DiceController jumpStartDice;

        // Phase2: airborne representation (Level=3) + continuous height for rendering/logic.
        // When jumpPhase != None, support=None and Level=3, while playerHeightNorm tracks the
        // current vertical position continuously (gravity-based jumpMotion.Offset).
        public int PlayerLevel { get; private set; }
        public float PlayerHeightNorm { get; private set; }
        public SupportRef PlayerSupport { get; private set; } = SupportRef.None();

        public bool IsOnFloor => standingController != null
            && standingController.IsOnFloor
            && !standingController.IsAirborne;
        public bool IsBusy => standingController != null
            && !IsOnFloor
            && standingController.CurrentDice != null
            && standingController.CurrentDice.IsMotionFollowActive;
        public bool IsCarrying => liftPhase != LiftPhase.None;
        public Vector2 FacePosition => standingController != null
            && standingController.TryGetStandingDice(out var standingDice)
            ? CharacterTransformDriver.GetOffsetFromDiceCenter(standingDice, characterTransform != null ? characterTransform.position : Vector3.zero)
            : Vector2.zero;
        public DiceController CurrentDice => standingController?.CurrentDice;
        public PlayerSlot PlayerSlot { get; private set; }
        public Vector2Int StandingGridCell => standingController != null ? standingController.GridCell : Vector2Int.zero;
        public CharacterPlacement StandingPlacement =>
            standingController != null ? standingController.Current : default;
        public bool IsJumping => jumpPhase != JumpPhase.None;
        public bool IsLiftCarrying => liftPhase == LiftPhase.Carrying;
        public bool IsLiftBusy => liftPhase == LiftPhase.Lifting || liftPhase == LiftPhase.Placing;

        ICharacterInputSource ActiveInputSource => inputSource != null ? inputSource : inputReader;

        public void SetInputSource(ICharacterInputSource source) {
            inputSource = source;
        }

        public void BindCrushOutcome(GameFlowController flow) {
            crushOutcomeFlow = flow;
        }

        public void Configure(
            Board targetBoard,
            PlacementService targetPlacement,
            DiceController startDice,
            CharacterMovementSettings movement,
            PhysicsSettings physics,
            PlayerSlot slot,
            PlayerInputSettings inputSettings,
            PlayerMatchActionContext actionContext = null,
            PlayerSlotInputConfig? inputConfigOverride = null) {
            board = targetBoard;
            placement = targetPlacement;
            registry = targetPlacement.Dice;
            matchActionContext = actionContext;
            movementSettings = movement;
            physicsSettings = physics;
            PlayerSlot = slot;

            if (inputReader == null) {
                inputReader = GetComponent<CharacterInputReader>();
            }

            if (inputSettings != null && inputReader != null && inputSource == null) {
                if (inputConfigOverride.HasValue) {
                    inputReader.Configure(slot, inputSettings, inputConfigOverride.Value);
                } else {
                    inputReader.Configure(slot, inputSettings);
                }
            }
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

            if (inputReader == null) {
                inputReader = GetComponent<CharacterInputReader>();
            }

            if (ActiveInputSource == null) {
                Debug.LogError("CharacterController: Character input source is not assigned.");
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
                () => standingController.SupportState,
                GetCharacterWorldY,
                () => coupling.IsTrackingRoll,
                PlayerSlot);

            movementTransition = placement.Passability;
            movementTransition.SetJumpParallelRollDebugLog(
                movementSettings.DebugJumpParallelRoll ? LogJumpParallelRoll : null);
            movementTransition.SetHeightTransferDebugLog(
                movementSettings.DebugMovementBlock ? LogHeightTransfer : null);

            coupling.Configure(
                board,
                registry,
                standingController,
                transformDriver,
                movementSettings,
                () => jumpYOffset,
                () => jumpMotion,
                matchActionContext,
                PlayerSlot);

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
            pendingJumpLandingState = new PendingJumpLandingState();
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

        public void OnStandingDiceErased(DiceController erasedDice) {
            if (!isInitialized || !standingController.TryGetStandingDice(out var standingDice) || standingDice != erasedDice) {
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

        public void OnStandingDiceBecameErasureGhost(DiceController ghostDice) {
            if (!isInitialized || !standingController.TryGetStandingDice(out var standingDice) || standingDice != ghostDice) {
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        void OnDisable() {
            coupling?.EndRollTracking();
            EndCarryState();
            if (isInitialized) {
                EndJump();
                standingController?.UnsubscribeAll();
            }
            EndPushFollow();
        }

        void Update() {
            if (!isInitialized || standingController == null) {
                return;
            }

            var input = ActiveInputSource.ReadMove();
            UpdateLastFacing(input);

            if (liftPhase == LiftPhase.Lifting || liftPhase == LiftPhase.Placing) {
                currentSpeed = 0f;
                return;
            }

            if (liftPhase == LiftPhase.Carrying) {
                currentSpeed = 0f;
                if (ActiveInputSource.TryGetDirectionPressedThisFrame(out var placeDirection)) {
                    TryPlaceCarriedDice(placeDirection);
                }

                return;
            }

            // If the player state indicates Top-support, but the top dice doesn't exist anymore,
            // fall under gravity instead of using any virtual-top height fallback.
            if (!isFalling
                && jumpPhase == JumpPhase.None
                && liftPhase == LiftPhase.None
                && standingController.Support.Kind == SupportKind.Dice
                && standingController.Support.DiceSurfaceLevel == DiceSurfaceLevel.Top
                && !registry.HasTopAt(standingController.GridCell)) {
                MoveToFloorAtCurrentWorldPosition();
                return;
            }

            // Ghost (pass-through) is never a standable surface.
            if (!isFalling
                && jumpPhase == JumpPhase.None
                && liftPhase == LiftPhase.None
                && standingController.TryGetStandingDice(out var passThroughStanding)
                && GhostPlacementRules.IsPlayerPassThrough(passThroughStanding)) {
                standingController.SetOnFloor(standingController.GridCell);
                transformDriver.SnapYToSurface();
                currentSpeed = 0f;
            }

            TryMountOntoCoveringDiceIfNeeded();

            if (ActiveInputSource.WasLiftPressedThisFrame()) {
                TryBeginLift();
            }

            if (jumpPhase == JumpPhase.None && ActiveInputSource.WasJumpPressedThisFrame()) {
                TryBeginJump();
            }

            if (standingController.CurrentDice != null
                && coupling.IsTrackingRoll
                && !standingController.CurrentDice.IsMotionFollowActive) {
                var wasArcRoll = coupling.CompleteRollIfFinished(standingController.CurrentDice);
                if (wasArcRoll && jumpPhase != JumpPhase.None) {
                    LogJump($"EndJump reason=arc-roll-complete {FormatJumpContext()}");
                    EndJump();
                }
            }

            if (isPushFollowing) {
                currentSpeed = 0f;
                return;
            }

            var isFollowingDiceMotion = standingController.CurrentDice != null
                && coupling.IsTrackingRoll
                && standingController.CurrentDice.IsMotionFollowActive;

            if (isFollowingDiceMotion) {
                if (standingController.CurrentDice.IsRolling) {
                    UpdateDuringRoll(input);
                } else {
                    currentSpeed = 0f;
                }
            } else {
                UpdateSurfaceMovement(input);
            }

            // Update jump after movement evaluation so "jumping" state isn't lost
            // on the same frame the player crosses a cell boundary.
            if (jumpPhase != JumpPhase.None) {
                UpdateJump();
                ApplyStandingDiceJumpVisual();
            }

            if (pendingEndJump && jumpPhase != JumpPhase.None) {
                EndJump();
            }

            UpdateFall();
            UpdatePlayerSupportAndHeightState();
        }

        void UpdatePlayerSupportAndHeightState() {
            if (board == null || standingController == null) {
                return;
            }

            var floorY = board.FloorSurfaceWorldY;
            var cellSize = board.CellSize;

            if (isFalling) {
                PlayerLevel = 3;
                PlayerSupport = SupportRef.None();
                PlayerHeightNorm = NormalizedHeight.ToNormalized(
                    fallGroundWorldY + fallMotion.Offset,
                    floorY,
                    cellSize);
                return;
            }

            if (jumpPhase != JumpPhase.None) {
                PlayerLevel = 3;
                PlayerSupport = SupportRef.None();

                // Footing height used by the character position:
                // - base is GetSurfaceWorldY (dice/stack/floor surface)
                // - add jumpYOffset only when the character view/feet are lifted over the surface
                var footWorldY = GetSurfaceWorldY();
                if (ShouldApplyJumpYOffsetToCharacter()) {
                    footWorldY += jumpYOffset;
                }

                PlayerHeightNorm = NormalizedHeight.ToNormalized(
                    footWorldY,
                    floorY,
                    cellSize);
                return;
            }

            if (IsOnFloor) {
                PlayerLevel = 0;
                PlayerSupport = SupportRef.Floor();
                PlayerHeightNorm = 0f;
                return;
            }

            if (standingController.IsAirborne) {
                PlayerLevel = 3;
                PlayerSupport = SupportRef.None();
                PlayerHeightNorm = NormalizedHeight.ToNormalized(
                    GetLogicalSurfaceWorldY(),
                    floorY,
                    cellSize);
                return;
            }

            PlayerLevel = standingController.Level;
            PlayerSupport = standingController.Support;

            if (standingController.TryGetStandingDice(out _)) {
                var footWorldY = GetLogicalSurfaceWorldY();
                PlayerHeightNorm = NormalizedHeight.ToNormalized(
                    footWorldY,
                    floorY,
                    cellSize);
            } else {
                // Defensive fallback: treat as airborne to avoid invalid support state.
                PlayerLevel = 3;
                PlayerSupport = SupportRef.None();
                PlayerHeightNorm = NormalizedHeight.ToNormalized(
                    GetLogicalSurfaceWorldY(),
                    floorY,
                    cellSize);
            }
        }

        void LateUpdate() {
            if (!isInitialized || standingController == null) {
                return;
            }

            if (isPushFollowing) {
                if (pushFollowLimitOneCell && HasExceededPushFollowOneCellLimit()) {
                    EndPushFollow();
                } else {
                    UpdatePushFollowPosition();
                    if (!pushFollowLimitOneCell
                        && (pushFollowDice == null || !pushFollowDice.IsRolling)) {
                        EndPushFollow();
                    }
                }
            }

            if (liftPhase == LiftPhase.Carrying && carriedDice != null) {
                carriedDice.View.SetCarryWorldPosition(GetCarryWorldPosition());
            }

            if (standingController.CurrentDice != null && coupling.IsTrackingRoll) {
                coupling.SyncVisual();
            } else if (!IsOnFloor || jumpPhase != JumpPhase.None) {
                transformDriver.SnapYToSurface();
            }
        }

        void UpdateSurfaceMovement(Vector2 input) {
            if (isFalling) {
                currentSpeed = 0f;
                return;
            }
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
            var fromLevel = standingController.Level;
            var footingWorldY = GetFootingWorldY();
            var walkHalfExtent = transformDriver.GetWalkHalfExtent();
            var rollTriggerHalfExtent = movementSettings.GetRollTriggerHalfExtent(walkHalfExtent);
            var nextXZ = currentXZ + move;

            if (IsOnFloor) {
                nextXZ = transformDriver.ClampToBoardBounds(nextXZ);
            }

            if (TryApplyPositionBasedMovement(
                currentXZ,
                ref nextXZ,
                move,
                standingCell,
                fromLevel,
                footingWorldY,
                walkHalfExtent,
                rollTriggerHalfExtent)) {
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
            int fromLevel,
            float footingWorldY,
            float walkHalfExtent,
            float rollTriggerHalfExtent) {
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
                fromLevel,
                footingWorldY,
                rollTriggerHalfExtent,
                standingController,
                isJumping,
                hasJumpCapability,
                jumpCapability,
                PlayerSlot);

            if (plan.Kind == CharacterMoveKind.Blocked) {
                LogPositionMovementBlock(
                    plan.Transition.Kind == MovementTransitionKind.BlockedStepOnly
                        ? "DissolveDescentBlocked"
                        : "TransitionBlocked",
                    plan.FromCell,
                    plan.ToCell,
                    fromLevel,
                    footingWorldY,
                    walkHalfExtent,
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
                fromLevel,
                footingWorldY,
                walkHalfExtent,
                isJumping,
                hasJumpCapability,
                jumpCapability,
                LogJumpParallelRoll,
                dissolveHoldState,
                pendingJumpLandingState,
                out var consumedMovement)) {
                if (consumedMovement) {
                    UpdatePushContact(Vector2.zero);
                }

                return consumedMovement;
            }

            return false;
        }

        bool TryGetJumpCoupledMoveCapability(out JumpCoupledMoveCapability capability) {
            var evaluated = JumpInputPolicy.TryEvaluate(
                jumpPhase != JumpPhase.None,
                coupling.JumpDiceGridMoved,
                physicsSettings,
                jumpMotion,
                GetDiceJumpHeight(),
                out capability);

            if (!evaluated || !capability.IsJumping) {
                return evaluated;
            }

            var standingDice = standingController.ResolveStandingDiceForMovement();
            capability = JumpInputPolicy.ApplyStandingDiceOverrides(capability, standingDice);

            if (coupling.JumpDiceGridMoved) {
                LogJumpParallelRoll(
                    "JumpInputPolicy blocked reason=already-moved " +
                    $"jumpPhase={jumpPhase} coupling.JumpDiceGridMoved={coupling.JumpDiceGridMoved}");
                return true;
            }

            if (!capability.AllowCrossCellMove) {
                LogJumpParallelRoll(
                    $"JumpInputPolicy blocked timeline={capability.Timeline:F3} " +
                    $"oneCellMax={physicsSettings.JumpGridMoveOneCellMaxTimeline:F3}");
                return true;
            }

            LogJumpParallelRoll(
                $"JumpInputPolicy allowed timeline={capability.Timeline:F3} " +
                $"maxDistance={capability.MaxDistance} allowTierChange={capability.AllowTierChange} " +
                $"twoCellMax={physicsSettings.JumpGridMoveTwoCellMaxTimeline:F3} " +
                $"oneCellMax={physicsSettings.JumpGridMoveOneCellMaxTimeline:F3}");
            return true;
        }

        void LogPositionMovementBlock(
            string reason,
            Vector2Int standingCell,
            Vector2Int nextCell,
            int fromLevel,
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
                fromLevel,
                fromSurfaceY,
                standingDice)
                ? DescribeWalkableTarget(standingCell, nextCell, fromLevel, fromSurfaceY)
                : "(none)";

            var detail =
                $"from={FormatMovementGrid(standingCell)} to={FormatMovementGrid(nextCell)} " +
                $"posCell={FormatMovementGrid(board.WorldToGrid(new Vector3(nextXZ.x, 0f, nextXZ.y)))} " +
                $"layer={fromLevel} tier={standingController.Tier} dice={FormatMovementDice(standingDice)} " +
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
            int fromLevel,
            float fromSurfaceY) {
            if (!MovementTransitionEvaluator.TryGetDirectionBetween(fromCell, toCell, out var direction)) {
                return "(none)";
            }

            var standingDice = standingController.ResolveStandingDiceForMovement();
            var transition = movementTransition.Evaluate(
                fromCell,
                fromLevel,
                direction,
                standingDice,
                PassabilityContext.ForGround(GetFootingWorldY(), PlayerSlot));
            if (transition.TargetLevel == SurfaceHeightLevel.Floor) {
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

        void LogJump(string message, bool throttle = false) {
            if (movementSettings == null || !movementSettings.DebugJump) {
                return;
            }

            if (throttle
                && message == debugLastJumpKey
                && Time.time - debugLastJumpLogTime < JumpLogInterval) {
                return;
            }

            debugLastJumpKey = message;
            debugLastJumpLogTime = Time.time;
            Debug.Log($"[Jump] {message}");
        }

        string FormatJumpContext() {
            var standingDice = standingController != null
                ? standingController.ResolveStandingDiceForMovement()
                : null;
            var diceLabel = standingDice != null
                ? $"{standingDice.name} kind={standingDice.Kind} canCouple={standingDice.CanJumpCoupleWithPlayer}"
                : "(none)";
            var grid = standingController != null
                ? FormatMovementGrid(standingController.GridCell)
                : "(?,?)";
            var layer = standingController != null ? standingController.Level : SurfaceHeightLevel.Floor;
            var tier = standingController != null ? standingController.Tier : DiceStackTier.Bottom;
            return
                $"phase={jumpPhase} grid={grid} layer={layer} tier={tier} " +
                $"onFloor={IsOnFloor} dice={diceLabel} yOffset={jumpYOffset:F3}";
        }

        void LogJumpYOffsetState() {
            if (jumpPhase == JumpPhase.None) {
                return;
            }

            var applyYOffset = ShouldApplyJumpYOffsetToCharacter();
            LogJump(
                $"JumpYOffset apply={applyYOffset} grounded={jumpMotion.IsGrounded} " +
                $"velocityY={jumpMotion.VelocityY:F3} {FormatJumpContext()}",
                throttle: true);
        }

        void LogHeightTransfer(string message) {
            if (!movementSettings.DebugMovementBlock) {
                return;
            }

            if (message == debugLastHeightTransferKey
                && Time.time - debugLastHeightTransferLogTime < HeightTransferLogInterval) {
                return;
            }

            debugLastHeightTransferKey = message;
            debugLastHeightTransferLogTime = Time.time;
            Debug.Log($"[HeightTransfer] {message}");
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

            var jumpPressed = ActiveInputSource.WasJumpPressedThisFrame();
            var halfExtent = transformDriver.GetWalkHalfExtent();
            coupling.TryHandleRollCancel(
                input,
                jumpPressed,
                GetFootingWorldY(),
                movementTransition,
                transformDriver.GetWorldXZ(),
                halfExtent,
                BeginJumpFromRollCancel,
                EndJump);
        }

        void BeginJumpFromRollCancel() {
            CaptureJumpStartPlacement();
            pendingJumpLandingState.Clear();
            jumpMotion = GravityMotion.CreateLaunch(GetDiceJumpHeight(), physicsSettings.Gravity);
            jumpPhase = JumpPhase.Airborne;
            jumpYOffset = 0f;
            coupling.ResetJumpSessionFlags();
            LogJump($"TryBeginJump ok source=roll-cancel {FormatJumpContext()}");
        }

        void MoveToFloorAtCurrentWorldPosition() {
            coupling?.EndRollTracking();

            var gridCell = characterTransform != null
                ? board.WorldToGrid(characterTransform.position)
                : standingController.GridCell;

            standingController.SetAirborne(gridCell);

            var targetY = ResolveFallTargetWorldY(gridCell);
            var characterY = characterTransform != null
                ? characterTransform.position.y
                : GetCharacterWorldY();
            var feetY = characterY - movementSettings.CharacterHeightOffset;

            var startOffset = Mathf.Max(0f, feetY - targetY);
            fallGroundWorldY = targetY;
            fallGridCell = gridCell;
            fallMotion = GravityMotion.CreateDrop(startOffset);

            if (fallMotion.IsGrounded) {
                isFalling = false;
                LandFromFall(gridCell);
                transformDriver?.SnapYToSurface();
                return;
            }

            isFalling = true;
        }

        /// <summary>
        /// If a dice occupies the level directly above the player on the same cell,
        /// crush (Iron / Stone) or mount onto that dice. Visual follow reuses DiceCharacterCoupling.
        /// Crush waits for full settle (spawn / demote / roll), including BottomEmergence.
        /// Erasing dice (sink / radiance) never crush — the player may occupy the same cell.
        /// </summary>
        void TryMountOntoCoveringDiceIfNeeded() {
            if (registry == null
                || standingController == null
                || isFalling
                || jumpPhase != JumpPhase.None
                || liftPhase != LiftPhase.None
                || isPushFollowing) {
                return;
            }

            if (!TryGetCoveringDice(out var coveringDice)) {
                return;
            }

            if (coveringDice.CrushesPlayerOnCover) {
                // Do not crush mid-motion (spawn / demote / roll), including BottomEmergence.
                // Do not crush while erasing (sink / radiance) — player may clip into that die.
                if (coveringDice.IsMotionFollowActive || coveringDice.IsErasing) {
                    return;
                }

                if (crushOutcomeFlow == null) {
                    Debug.LogError("CharacterController: Crush outcome flow is not bound.");
                    return;
                }

                crushOutcomeFlow.NotifyPlayerCrushed(PlayerSlot);
                return;
            }

            if (BlocksCoveringMountUntilSettled(coveringDice)) {
                return;
            }

            MountOntoCoveringDice(coveringDice);
        }

        bool TryGetCoveringDice(out DiceController coveringDice) {
            coveringDice = null;
            var cell = standingController.GridCell;
            var level = standingController.Level;

            if (SurfaceHeightLevel.IsFloor(level)) {
                if (!registry.TryGetBottomIncludingPending(cell, out var bottom)
                    || bottom == null
                    || bottom == standingController.CurrentDice
                    || GhostPlacementRules.IsPlayerPassThrough(bottom)) {
                    return false;
                }

                coveringDice = bottom;
                return true;
            }

            if (level == SurfaceHeightLevel.Bottom) {
                if (!registry.TryGetTopAt(cell, out var top)
                    || top == null
                    || top == standingController.CurrentDice
                    || GhostPlacementRules.IsPlayerPassThrough(top)) {
                    return false;
                }

                coveringDice = top;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Natural spawn fall / demote / roll: wait until motion ends, then mount if still same cell.
        /// Bottom emergence allows mount immediately via <see cref="DiceController.AllowsUnconditionalMount"/>.
        /// </summary>
        static bool BlocksCoveringMountUntilSettled(DiceController dice) {
            return dice != null
                && dice.IsMotionFollowActive
                && !dice.AllowsUnconditionalMount;
        }

        void MountOntoCoveringDice(DiceController coveringDice) {
            if (coveringDice == null) {
                Debug.LogError("CharacterController: MountOntoCoveringDice received null dice.");
                return;
            }

            if (GhostPlacementRules.IsPlayerPassThrough(coveringDice)) {
                return;
            }

            var state = coveringDice.CurrentState;
            standingController.SetOnDice(state.GridPos, state.Tier, coveringDice);
            currentSpeed = 0f;

            if (coveringDice.IsMotionFollowActive) {
                coupling.BeginMountFollow();
            } else {
                transformDriver.SnapYToSurface();
            }
        }

        float ResolveFallTargetWorldY(Vector2Int cell) {
            return PlayerNaturalLanding.ResolveSurfaceWorldY(cell, registry, board);
        }

        void LandFromFall(Vector2Int cell) {
            standingController.ApplySupportState(PlayerNaturalLanding.Resolve(cell, registry));
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
            if (board == null) {
                return 0f;
            }

            if (IsOnFloor) {
                return board.FloorSurfaceWorldY;
            }

            if (standingController != null && standingController.TryGetStandingDice(out var standingDice)) {
                return standingDice.GetTopSurfaceWorldY();
            }

            return board.FloorSurfaceWorldY;
        }

        float GetLogicalSurfaceWorldY() {
            if (board == null) {
                return 0f;
            }

            if (IsOnFloor) {
                return board.FloorSurfaceWorldY;
            }

            if (standingController != null && standingController.TryGetStandingDice(out var standingDice)) {
                return standingDice.GetLogicalTopSurfaceWorldY();
            }

            if (isFalling) {
                return fallGroundWorldY + fallMotion.Offset;
            }

            return board.FloorSurfaceWorldY;
        }

        float GetFootingWorldY() {
            return GetLogicalSurfaceWorldY();
        }

        float GetCharacterWorldY() {
            if (isFalling) {
                return fallGroundWorldY + movementSettings.CharacterHeightOffset + fallMotion.Offset;
            }

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

            if (!standingController.TryGetStandingDice(out var standingDice)) {
                return false;
            }

            if (!standingDice.CanJumpCoupleWithPlayer) {
                return true;
            }

            return standingDice.IsSinkErasing;
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

            if (PlayerMotionGate.BlocksPushContact(
                PlayerSlot,
                matchActionContext,
                registry,
                board,
                StandingGridCell,
                standingController != null ? standingController.CurrentDice : null)) {
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
                if (TryPushDice(candidate, out var pushedDice, out var pushDir)) {
                    matchActionContext?.RegisterActionDice(pushedDice, PlayerSlot);
                    LogPushDebug(
                        "push-ok",
                        $"stage=push dice={FormatMovementDice(pushedDice)} dir={pushDir}");
                    BeginPushFollow(pushedDice, pushDir);
                    pushed = true;
                    break;
                }

                LogPushDebug(
                    $"push-fail-{FormatMovementDice(candidate.Dice)}-{candidate.Direction}",
                    $"stage=push dice={FormatMovementDice(candidate.Dice)} dir={candidate.Direction} push-failed");
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
            pushFollowLimitOneCell = CharacterPushExecutor.LimitsPushFollowToOneCell(dice);
            pushFollowDiceStartWorld = dice?.View?.DiceTransform != null
                ? dice.View.DiceTransform.position
                : Vector3.zero;
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
            pushFollowLimitOneCell = false;
            pushFollowDice = null;
        }

        void OnPushFollowDiceStateChanged(DiceState state) {
            if (!isPushFollowing) {
                return;
            }

            EndPushFollow();
        }

        bool HasExceededPushFollowOneCellLimit() {
            if (!pushFollowLimitOneCell
                || pushFollowDice?.View?.DiceTransform == null
                || board == null) {
                return false;
            }

            var delta = pushFollowDice.View.DiceTransform.position - pushFollowDiceStartWorld;
            var displacement = pushFollowDirection switch {
                Direction.East => delta.x,
                Direction.West => -delta.x,
                Direction.North => delta.z,
                Direction.South => -delta.z,
                _ => 0f
            };

            return displacement >= board.CellSize - EdgeEpsilon;
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

            var diceCell = pushFollowLimitOneCell && pushFollowDice.View?.DiceTransform != null
                ? board.WorldToGrid(pushFollowDice.View.DiceTransform.position)
                : pushFollowDice.CurrentState.GridPos;
            var contactCell = diceCell + pushFollowDirection.Opposite().ToGridDelta();
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
                if (GhostPlacementRules.IsPlayerPassThrough(bottom)
                    || BlocksCoveringMountUntilSettled(bottom)) {
                    ApplyFloorStanding(gridCell);
                    return;
                }

                ApplyDiceStanding(gridCell, DiceStackTier.Bottom, bottom);
                return;
            }

            ApplyFloorStanding(gridCell);
        }

        void ApplyFloorStanding(Vector2Int gridCell) {
            if (standingController.Level == SurfaceHeightLevel.Floor
                && standingController.GridCell == gridCell
                && standingController.CurrentDice == null) {
                return;
            }

            standingController.SetOnFloor(gridCell);
        }

        void ApplyDiceStanding(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            var level = SurfaceHeightLevel.FromDiceStackTier(tier); if (standingController.Level == level
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
                if (GhostPlacementRules.IsPlayerPassThrough(pushBody.Dice)) {
                    overlapSummary.Append($" [{diceLabel}:ghost-pass-through]");
                    continue;
                }

                if (pushBody.Dice.IsVanishing || pushBody.Dice.IsBusy) {
                    overlapSummary.Append($" [{diceLabel}:busy]");
                    continue;
                }

                if (pushBody.Dice.IsSinkErasing) {
                    overlapSummary.Append($" [{diceLabel}:sink-erasing]");
                    continue;
                }

                if (!CanPushDice(pushBody.Dice, out var rejectReason)) {
                    overlapSummary.Append($" [{diceLabel}:canPush={rejectReason}]");
                    continue;
                }

                overlapSummary.Append($" [{diceLabel}:canPush=ok");
                foreach (Direction direction in new[] {
                    Direction.East, Direction.West, Direction.North, Direction.South }) {
                    if (PushContactEvaluator.TryEvaluate(
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
                var minAlignment = movementSettings.PushInputAlignment;
                PushContactEvaluator.TryAdd(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.East, minAlignment);
                PushContactEvaluator.TryAdd(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.West, minAlignment);
                PushContactEvaluator.TryAdd(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.North, minAlignment);
                PushContactEvaluator.TryAdd(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.South, minAlignment);
            }

            candidates.Sort(PushContactEvaluator.Compare);

            LogPushDebugWhenInput(
                input,
                "overlap-summary",
                $"stage=overlap standing={FormatMovementGrid(standingController.GridCell)} layer={standingController.Level} " +
                $"tier={standingController.Tier} charXZ=({characterXZ.x:F2},{characterXZ.y:F2}) input=({input.x:F2},{input.y:F2}) " +
                $"hits={hits.Length} dice={overlapSummary} candidates={candidates.Count}");
        }

        bool TryPushDice(PushContactCandidate candidate, out DiceController pushedDice, out Direction pushDir) {
            pushedDice = null;
            pushDir = candidate.Direction;
            if (!CharacterPushExecutor.TryExecute(
                candidate.Dice,
                candidate.Direction,
                registry,
                movementTransition,
                GetFootingWorldY(),
                PlayerSlot)) {
                return false;
            }

            pushedDice = candidate.Dice;
            return true;
        }

        bool CanPushDice(DiceController dice) {
            return CanPushDice(dice, out _);
        }

        bool CanPushDice(DiceController dice, out string rejectReason) {
            return PushPassability.CanPush(
                standingController.Current,
                IsOnFloor,
                standingController.ResolveStandingDiceForMovement(),
                dice,
                registry,
                out rejectReason);
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

            if (PlayerMotionGate.BlocksJumpOrLiftStart(
                PlayerSlot,
                matchActionContext,
                registry,
                board,
                StandingGridCell,
                standingController != null ? standingController.CurrentDice : null)) {
                return false;
            }

            var input = ActiveInputSource.ReadMove();
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

            if (!CarryPlacementPassability.TryResolveTarget(targetGrid, registry, out var targetTier, out _)) {
                return false;
            }

            liftPhase = LiftPhase.Placing;
            var fromWorld = GetCarryWorldPosition();
            var placesOnSinkErasingBottom = targetTier == DiceStackTier.Top
                && registry.TryGetBottomAt(targetGrid, out var sinkingBottom)
                && sinkingBottom != null
                && sinkingBottom.IsSinkErasing;

            if (!placesOnSinkErasingBottom) {
                matchActionContext?.RegisterActionDice(carriedDice, PlayerSlot);
            }

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

            if (registry == null || board == null || !hasLastFacing || standingController == null) {
                return false;
            }

            var neighborGrid = standingController.GridCell + lastFacing.ToGridDelta();
            return CharacterLiftTargetQuery.TryResolve(
                standingController.Current,
                IsOnFloor,
                standingController.ResolveStandingDiceForMovement(),
                neighborGrid,
                board,
                registry,
                out targetDice);
        }

        bool TryBeginJump() {
            if (jumpPhase != JumpPhase.None) {
                LogJump($"TryBeginJump rejected reason=already-jumping {FormatJumpContext()}");
                return false;
            }

            if (liftPhase != LiftPhase.None) {
                LogJump($"TryBeginJump rejected reason=lift-active liftPhase={liftPhase} {FormatJumpContext()}");
                return false;
            }

            if (isPushFollowing) {
                LogJump($"TryBeginJump rejected reason=push-following {FormatJumpContext()}");
                return false;
            }

            if (IsPlayerMotionGateBlockingInput(out var jumpBlockReason)) {
                LogJump($"TryBeginJump rejected reason={jumpBlockReason} {FormatJumpContext()}");
                return false;
            }

            jumpMotion = GravityMotion.CreateLaunch(GetDiceJumpHeight(), physicsSettings.Gravity);
            jumpPhase = JumpPhase.Airborne;
            jumpYOffset = 0f;
            pendingJumpLandingState.Clear();
            CaptureJumpStartPlacement();
            coupling.ResetJumpSessionFlags();
            ResetPushState();
            LogJump($"TryBeginJump ok source=key {FormatJumpContext()}");
            return true;
        }

        void CaptureJumpStartPlacement() {
            hasJumpStartPlacement = standingController != null
                && standingController.TryGetStandingDice(out jumpStartDice);
            jumpStartGridCell = standingController != null
                ? standingController.GridCell
                : default;
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
            LogJumpYOffsetState();

            if (jumpMotion.IsGrounded) {
                if (coupling.IsJumpArc && standingController.CurrentDice != null && standingController.CurrentDice.IsRolling) {
                    LogJump(
                        $"UpdateJump hold reason=jump-arc-dice-rolling dice={standingController.CurrentDice.name} " +
                        $"{FormatJumpContext()}",
                        throttle: true);
                    return;
                }

                LogJump($"EndJump reason=grounded {FormatJumpContext()}");
                pendingEndJump = true;
            }
        }

        void UpdateFall() {
            if (!isFalling) {
                return;
            }

            fallMotion = GravityMotion.Step(fallMotion, physicsSettings.Gravity, Time.deltaTime);
            if (fallMotion.IsGrounded) {
                isFalling = false;
                coupling?.EndRollTracking();
                LandFromFall(fallGridCell);
                transformDriver?.SnapYToSurface();
            }
        }

        void ApplyStandingDiceJumpVisual() {
            if (jumpPhase == JumpPhase.None || board == null) {
                return;
            }

            var targetDice = ResolveJumpVisualDice();
            if (targetDice != null && targetDice.IsRolling) {
                return;
            }

            if (jumpVisualDice != null && jumpVisualDice != targetDice) {
                ClearJumpVisualDice(jumpVisualDice);
            }

            jumpVisualDice = targetDice;
            if (targetDice == null) {
                return;
            }

            if (!targetDice.CanJumpCoupleWithPlayer) {
                ClearJumpVisualDice(targetDice);
                jumpVisualDice = null;
                return;
            }

            if (coupling.JumpMoveKind == JumpDiceMoveKind.StackOntoTop) {
                targetDice.View.ClearVisualYOffset(board);
                return;
            }

            targetDice.View.ApplyVisualYOffset(board, jumpYOffset);
            if (pendingJumpLandingState.HasPending) {
                return;
            }

            if (standingController.Tier == DiceStackTier.Bottom && registry.HasTopAt(standingController.GridCell)) {
                registry.SyncStackedTopAt(standingController.GridCell, board);
            }
        }

        DiceController ResolveJumpVisualDice() {
            if (coupling.JumpDiceGridMoved
                && standingController.TryGetStandingDice(out var rollingDice)
                && !rollingDice.IsErasing) {
                return rollingDice;
            }

            if (jumpStartDice != null && !jumpStartDice.IsErasing) {
                return jumpStartDice;
            }

            if (standingController.TryGetStandingDice(out var standingDice) && !standingDice.IsErasing) {
                return standingDice;
            }

            return null;
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
            pendingJumpLandingState.TryCommit(standingController.ApplyFromTransition);
            TryApplyJumpLandingSinkCompression();
            MarkSameCellJumpPlacement();

            if (jumpVisualDice != null) {
                ClearJumpVisualDice(jumpVisualDice);
                jumpVisualDice = null;
            }

            jumpPhase = JumpPhase.None;
            pendingEndJump = false;
            jumpMotion = new VerticalMotionState {
                Offset = 0f,
                VelocityY = 0f,
                IsGrounded = true
            };
            jumpYOffset = 0f;
            hasJumpStartPlacement = false;
            jumpStartDice = null;
            pendingJumpLandingState.Clear();
            coupling?.ResetJumpSessionFlags();
            transformDriver?.SnapYToSurface();
        }

        void TryApplyJumpLandingSinkCompression() {
            if (!hasJumpStartPlacement
                || jumpStartDice == null
                || standingController == null
                || registry == null
                || movementSettings == null) {
                return;
            }

            if (standingController.GridCell != jumpStartGridCell) {
                return;
            }

            if (!standingController.TryGetStandingDice(out var landingDice)
                || landingDice != jumpStartDice) {
                return;
            }

            if (jumpStartDice.CurrentState.Tier != DiceStackTier.Top
                || !registry.TryGetBottomAt(jumpStartGridCell, out var sinkBottom)
                || sinkBottom == null
                || sinkBottom == jumpStartDice
                || !sinkBottom.IsSinkErasing
                || sinkBottom.Capabilities.BlocksJumpLandingSinkAdvance) {
                return;
            }

            var advance = movementSettings.JumpLandingSinkAdvance;
            if (advance <= 0f) {
                return;
            }

            sinkBottom.AdvanceErasure(advance);
        }

        void MarkSameCellJumpPlacement() {
            if (!hasJumpStartPlacement || standingController == null) {
                return;
            }

            // Iron / Stone / iron-adjacent Magnet: player-only jump is not a dice action.
            if (JumpPlayerTransferPolicy.UsesPlayerOnlyReach(isJumping: true, jumpStartDice)) {
                return;
            }

            if (standingController.GridCell != jumpStartGridCell) {
                return;
            }

            if (!standingController.TryGetStandingDice(out var landingDice) || landingDice != jumpStartDice) {
                return;
            }

            matchActionContext?.RegisterActionDice(landingDice, PlayerSlot);
            matchActionContext?.NotifyParticipantMoveCompleted(PlayerSlot);
        }

        public static Vector2 DirectionToMoveVector(Direction direction) {
            return direction switch {
                Direction.East => Vector2.right,
                Direction.West => Vector2.left,
                Direction.North => new Vector2(0f, 1f),
                Direction.South => new Vector2(0f, -1f),
                _ => Vector2.zero
            };
        }

        public bool TryEvaluateMove(Vector2 moveInput, out CharacterMovePlan plan) {
            plan = default;
            if (!isInitialized || movePlanner == null || standingController == null || transformDriver == null) {
                return false;
            }

            var currentXZ = transformDriver.GetWorldXZ();
            var standingCell = standingController.GridCell;
            var fromLevel = standingController.Level;
            var footingWorldY = GetFootingWorldY();
            var walkHalfExtent = transformDriver.GetWalkHalfExtent();
            var rollTriggerHalfExtent = movementSettings.GetRollTriggerHalfExtent(walkHalfExtent);
            var isJumping = jumpPhase != JumpPhase.None;
            var hasJumpCapability = false;
            JumpCoupledMoveCapability jumpCapability = default;
            if (isJumping) {
                hasJumpCapability = TryGetJumpCoupledMoveCapability(out jumpCapability);
            }

            plan = movePlanner.TryBuildPlan(
                currentXZ,
                moveInput,
                standingCell,
                fromLevel,
                footingWorldY,
                rollTriggerHalfExtent,
                standingController,
                isJumping,
                hasJumpCapability,
                jumpCapability,
                PlayerSlot);
            return true;
        }

        public bool CanLiftTarget(DiceController dice) {
            if (dice == null || registry == null || standingController == null) {
                return false;
            }

            if (dice.IsErasing || dice.IsVanishing || dice.IsBusy) {
                return false;
            }

            return LiftPassability.CanLift(
                standingController.Current,
                IsOnFloor,
                standingController.ResolveStandingDiceForMovement(),
                dice,
                registry);
        }

        public bool CanPlaceCarriedAt(Direction direction) {
            if (liftPhase != LiftPhase.Carrying || carriedDice == null || board == null) {
                return false;
            }

            var targetGrid = standingController.GridCell + direction.ToGridDelta();
            return CarryPlacementPassability.TryResolveTarget(targetGrid, registry, out _, out _);
        }

        public bool IsReadyForAiPlanning() {
            return isInitialized
                && liftPhase != LiftPhase.Lifting
                && liftPhase != LiftPhase.Placing
                && jumpPhase == JumpPhase.None
                && !isPushFollowing
                && !isFalling
                && !IsPlayerMotionGateBlockingInput(out _)
                && !IsBusy;
        }

        bool IsPlayerMotionGateBlockingInput(out string reason) {
            return PlayerMotionGate.TryGetBlockReason(
                PlayerSlot,
                matchActionContext,
                registry,
                board,
                StandingGridCell,
                standingController != null ? standingController.CurrentDice : null,
                out reason);
        }

        public bool AnyActionDiceRollingForPlayer() {
            return matchActionContext != null && matchActionContext.AnyRollingForPlayer(PlayerSlot);
        }

        public bool TryGetAiNavigationQuery(
            out MovementTransitionEvaluator passability,
            out float footingWorldY) {
            if (!isInitialized || movementTransition == null) {
                passability = null;
                footingWorldY = 0f;
                return false;
            }

            passability = movementTransition;
            footingWorldY = GetFootingWorldY();
            return true;
        }

        public AiNavigationState GetAiNavigationState() {
            return new AiNavigationState(
                StandingGridCell,
                StandingPlacement.Level,
                CurrentDice);
        }

        public Vector2 GetStandingWorldXZ() {
            return transformDriver != null ? transformDriver.GetWorldXZ() : Vector2.zero;
        }

        public bool IsNearCellCenter(Vector2Int cell, float tolerance) {
            if (transformDriver == null) {
                return false;
            }

            var delta = transformDriver.GetWorldXZ() - transformDriver.GetCellCenterXZ(cell);
            return delta.sqrMagnitude <= tolerance * tolerance;
        }

        public float GetDistanceToCellCenter(Vector2Int cell) {
            if (transformDriver == null) {
                return float.MaxValue;
            }

            return Vector2.Distance(transformDriver.GetWorldXZ(), transformDriver.GetCellCenterXZ(cell));
        }
    }
}
