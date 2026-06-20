using System.Collections.Generic;
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
        [SerializeField] float characterHeightOffset = 0.15f;
        [SerializeField] float maxMoveSpeed = 2.5f;
        [SerializeField] float moveAcceleration = 10f;
        [SerializeField] float rollCenterPullSpeed = 2.5f;
        [SerializeField] float maxStepHeight = 1.5f;
        [SerializeField] float pushHoldDuration = 0.25f;
        [SerializeField] float pushInputAlignment = 0.7f;
        [SerializeField] KeyCode liftKey = KeyCode.Q;
        [SerializeField] float carryVerticalOffset = 1.05f;
        [SerializeField] bool debugMovementBlock;

        const float MovementBlockLogInterval = 0.25f;

        MovementTransitionEvaluator movementTransition;
        string debugLastMovementBlockKey;
        float debugLastMovementBlockLogTime = -1f;

        enum LiftPhase {
            None,
            Lifting,
            Carrying,
            Placing
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
        float rollFixedWorldY;
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

        public void Configure(Board targetBoard, DiceRegistry targetRegistry, DiceController startDice) {
            board = targetBoard;
            registry = targetRegistry;
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

            if (currentDice != null) {
                currentDice.View.EnsureDiceInstance();
                if (currentDice.View.DiceTransform == null) {
                    Debug.LogError("CharacterController: Dice visual is not available.");
                    return;
                }
            }

            EnsureCharacterInstance();
            EnsureCharacterPushCollider();
            movementTransition = new MovementTransitionEvaluator(board, registry, maxStepHeight);
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

        void OnDisable() {
            EndRollTracking();
            EndCarryState();
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

            if (Input.GetKeyDown(liftKey)) {
                TryBeginLift();
            }

            if (isPushFollowing) {
                currentSpeed = 0f;
                return;
            }

            if (!IsOnFloor && currentDice != null && isTrackingDiceRoll && !currentDice.IsRolling) {
                EndRollTracking();
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
            } else if (!IsOnFloor) {
                SnapYToSurface();
            }
        }

        void UpdateSurfaceMovement(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                currentSpeed = 0f;
                ResetPushState();
                return;
            }

            input.Normalize();
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, moveAcceleration * Time.deltaTime);

            if (currentSpeed <= 0f) {
                return;
            }

            var move = input * (currentSpeed * Time.deltaTime);
            SyncStandingDiceCache();
            var currentXZ = GetWorldXZ();
            var standingCell = GetStandingCell();
            var fromLayer = GetCurrentLayer();
            var fromSurfaceY = GetSurfaceWorldY();
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

            if (!MovementTransitionEvaluator.IsOrthogonalAdjacent(standingCell, nextCell)) {
                nextXZ = ClampToCellInterior(nextXZ, standingCell, halfExtent);

                return false;
            }

            if (!MovementTransitionEvaluator.TryGetDirectionBetween(standingCell, nextCell, out var direction)) {
                return false;
            }

            var standingDice = ResolveStandingDiceForMovement();
            var transition = movementTransition.Evaluate(
                standingCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standingTier);

            switch (transition.Kind) {
                case MovementTransitionKind.Walkable:
                    ApplyTransitionStanding(transition, nextCell);
                    return false;
                case MovementTransitionKind.CanRoll:
                    if (TryGetPrimaryDirection(move, out var moveDir) && moveDir == direction) {
                        if (TryExecuteRoll(direction, nextXZ, halfExtent)) {
                            UpdatePushContact(Vector2.zero);
                            return true;
                        }

                        if (Mathf.Abs(fromSurfaceY - board.FloorSurfaceWorldY) <= maxStepHeight) {
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

        Vector2Int XZToGrid(Vector2 xz) {
            return board.WorldToGrid(new Vector3(xz.x, 0f, xz.y));
        }

        Vector2Int ResolveNextCell(
            Vector2Int standingCell,
            Vector2 currentXZ,
            Vector2 nextXZ,
            Vector2 move,
            float halfExtent) {
            if (TryGetPrimaryDirection(move, out var moveDir)) {
                if (IsAtOrPastFaceEdge(currentXZ, standingCell, moveDir, halfExtent)) {
                    return standingCell + moveDir.ToGridDelta();
                }

                return standingCell;
            }

            var positionCell = XZToGrid(nextXZ);
            if (positionCell == standingCell) {
                return standingCell;
            }

            if (MovementTransitionEvaluator.IsOrthogonalAdjacent(standingCell, positionCell)) {
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
            if (!debugMovementBlock) {
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
            if (!debugMovementBlock) {
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
                return registry.TryGetTopAt(standingGridCell, out dice);
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

        bool TryExecuteRoll(Direction direction, Vector2 nextXZ, float edgeLimit) {
            SyncStandingDiceCache();
            if (currentDice?.View.DiceTransform == null || currentDice.IsDissolving) {
                return false;
            }

            if (standingTier != DiceStackTier.Bottom
                || currentDice.CurrentState.Tier != DiceStackTier.Bottom
                || registry.HasTopAt(standingGridCell)) {
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

            if (!currentDice.TryRoll(direction)) {
                return false;
            }

            standingGridCell = currentDice.CurrentState.GridPos;
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
                return standingDice.GetTopSurfaceWorldY();
            }

            return board.FloorSurfaceWorldY;
        }

        void ApplyWorldPosition(Vector3 worldPos) {
            if (characterTransform == null || board == null) {
                return;
            }

            worldPos.y = GetSurfaceWorldY() + characterHeightOffset;
            characterTransform.position = worldPos;
            characterTransform.rotation = Quaternion.identity;
        }

        void SnapYToSurface() {
            if (characterTransform == null || isTrackingDiceRoll) {
                return;
            }

            var position = characterTransform.position;
            position.y = GetSurfaceWorldY() + characterHeightOffset;
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
            rollFixedWorldY = rollStartCharacterPosition.y;
            isTrackingDiceRoll = true;
        }

        void BeginRollTracking(Vector3 characterAnchor, Vector3 diceCenterAnchor) {
            rollStartCharacterPosition = characterAnchor;
            rollStartDiceCenter = diceCenterAnchor;
            rollFixedWorldY = characterAnchor.y;
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
            worldPosition.y = rollFixedWorldY;
            characterTransform.position = worldPosition;
            characterTransform.rotation = Quaternion.identity;
        }

        void ResetPushState() {
            pushContactTime = 0f;
            pushTargetDice = null;
            hasPushDirection = false;
        }

        void UpdatePushContact(Vector2 input) {
            if (liftPhase != LiftPhase.None) {
                return;
            }

            if (registry == null || registry.AnyRolling() || registry.AnyCarried()) {
                ResetPushState();
                return;
            }

            CollectPushCandidates(input, pushCandidates);
            if (pushCandidates.Count == 0) {
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
            }

            pushContactTime += Time.deltaTime;
            if (pushContactTime < pushHoldDuration) {
                return;
            }

            foreach (var candidate in pushCandidates) {
                if (candidate.Dice.TrySlide(candidate.Direction)) {
                    BeginPushFollow(candidate.Dice, candidate.Direction);
                    break;
                }
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

            foreach (var hit in hits) {
                if (hit == characterPushCollider) {
                    continue;
                }

                var pushBody = hit.GetComponent<DicePushBody>();
                if (pushBody == null || pushBody.Dice == null || pushBody.Collider == null) {
                    continue;
                }

                if (pushBody.Dice.IsDissolving || pushBody.Dice.IsBusy) {
                    continue;
                }

                if (!CanPushDice(pushBody.Dice)) {
                    continue;
                }

                var pushBounds = pushBody.Collider.bounds;
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.East);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.West);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.North);
                TryAddPushCandidate(candidates, pushBody.Dice, pushBounds, input, characterXZ, Direction.South);
            }

            candidates.Sort(ComparePushCandidates);
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
                pushInputAlignment,
                out var inputAlignment,
                out var faceDistance)) {
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
            out float faceDistance) {
            inputAlignment = Vector2.Dot(input, GetDirectionInputVector(direction));
            if (inputAlignment < minInputAlignment) {
                faceDistance = 0f;
                return false;
            }

            var charX = characterPosition.x;
            var charZ = characterPosition.y;

            switch (direction) {
                case Direction.East:
                    if (charX > bounds.center.x + EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.min.x);
                    break;
                case Direction.West:
                    if (charX < bounds.center.x - EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.max.x);
                    break;
                case Direction.North:
                    if (charZ > bounds.center.z + EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.min.z);
                    break;
                case Direction.South:
                    if (charZ < bounds.center.z - EdgeEpsilon) {
                        faceDistance = 0f;
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.max.z);
                    break;
                default:
                    faceDistance = 0f;
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
            if (dice == null || registry == null) {
                return false;
            }

            if (TryGetStandingDice(out var standingDice) && dice == standingDice) {
                return false;
            }

            if (IsOnFloor) {
                return dice.CurrentState.Tier == DiceStackTier.Bottom
                    && !registry.HasTopAt(dice.CurrentState.GridPos);
            }

            return dice.CurrentState.Tier == DiceStackTier.Top;
        }

        bool CanLiftDice(DiceController dice) {
            if (dice == null || registry == null) {
                return false;
            }

            if (TryGetStandingDice(out var standingDice) && dice == standingDice) {
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

        Vector3 GetCarryWorldPosition() {
            if (characterTransform == null) {
                return Vector3.zero;
            }

            var position = characterTransform.position;
            return new Vector3(position.x, position.y + carryVerticalOffset, position.z);
        }

        bool TryBeginLift() {
            if (liftPhase != LiftPhase.None || isPushFollowing) {
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

            var originGrid = board.WorldToGrid(characterTransform.position);
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
            if (registry.TryGetTopAt(neighborGrid, out var top)) {
                return top;
            }

            if (registry.TryGetBottomAt(neighborGrid, out var bottom)) {
                return bottom;
            }

            return null;
        }
    }
}
