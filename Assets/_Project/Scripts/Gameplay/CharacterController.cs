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
        [SerializeField] float faceStepRatio = 0.85f;
        [SerializeField] float maxMoveSpeed = 2.5f;
        [SerializeField] float moveAcceleration = 10f;
        [SerializeField] float rollCenterPullSpeed = 2.5f;
        [SerializeField] float maxStepHeight = 1.5f;

        DiceRegistry registry;
        DiceController currentDice;
        Transform characterTransform;
        Vector2 facePosition;
        Vector2 floorWorldPosition;
        float currentSpeed;
        bool isInitialized;

        public bool IsOnFloor => currentDice == null;
        public bool IsBusy => currentDice != null && currentDice.IsRolling;
        public Vector2 FacePosition => facePosition;
        public DiceController CurrentDice => currentDice;

        public void Configure(Board targetBoard, DiceRegistry targetRegistry, DiceController startDice) {
            board = targetBoard;
            registry = targetRegistry;
            SetCurrentDice(startDice);
            Initialize();
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
            facePosition = Vector2.zero;
            currentSpeed = 0f;
            isInitialized = true;
            UpdateCharacterWorldPosition();
        }

        public void OnStandingDiceDissolved(DiceController dissolvedDice) {
            if (!isInitialized || currentDice != dissolvedDice) {
                return;
            }

            MoveToFloorAtCurrentWorldPosition();
        }

        void OnDisable() {
            UnsubscribeCurrentDice();
        }

        void Update() {
            if (!isInitialized) {
                return;
            }

            var input = GetInputDirection();
            var isRolling = currentDice != null && currentDice.IsRolling;

            if (isRolling) {
                UpdateDuringRoll(input);
            } else if (IsOnFloor) {
                UpdateFloorMovement(input);
            } else {
                UpdateDiceMovement(input);
            }
        }

        void LateUpdate() {
            if (!isInitialized) {
                return;
            }

            UpdateCharacterWorldPosition();
        }

        void UpdateDiceMovement(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                currentSpeed = 0f;
                return;
            }

            input.Normalize();
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, moveAcceleration * Time.deltaTime);

            if (currentSpeed <= 0f) {
                return;
            }

            var edgeLimit = GetEdgeLimit();
            var move = input * (currentSpeed * Time.deltaTime);
            var nextPosition = facePosition + move;

            if (TryTransferToAdjacentDiceAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            if (TryRollAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            if (TryTransferToFloorAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            facePosition = ClampToFace(nextPosition, edgeLimit);
        }

        void UpdateFloorMovement(Vector2 input) {
            if (input.sqrMagnitude <= 0f) {
                currentSpeed = 0f;
                return;
            }

            input.Normalize();
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, moveAcceleration * Time.deltaTime);

            if (currentSpeed <= 0f) {
                return;
            }

            var move = input * (currentSpeed * Time.deltaTime);
            var nextPosition = floorWorldPosition + move;

            if (TryStepOntoDiceFromFloor(nextPosition, move)) {
                return;
            }

            floorWorldPosition = ClampFloorPosition(nextPosition);
        }

        void UpdateDuringRoll(Vector2 input) {
            if (input.sqrMagnitude > 0f) {
                input.Normalize();
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, moveAcceleration * Time.deltaTime);
                var move = input * (currentSpeed * Time.deltaTime);
                facePosition = ClampToFace(facePosition + move, GetEdgeLimit());
                return;
            }

            currentSpeed = 0f;
            facePosition = Vector2.MoveTowards(facePosition, Vector2.zero, rollCenterPullSpeed * Time.deltaTime);
        }

        void OnDiceStateChanged(DiceState state) {
            if (!isInitialized) {
                return;
            }

            currentSpeed = 0f;
        }

        void SetCurrentDice(DiceController dice) {
            UnsubscribeCurrentDice();
            currentDice = dice;
            if (currentDice != null) {
                currentDice.StateChanged += OnDiceStateChanged;
            }
        }

        void UnsubscribeCurrentDice() {
            if (currentDice != null) {
                currentDice.StateChanged -= OnDiceStateChanged;
            }
        }

        void MoveToFloorAtCurrentWorldPosition() {
            if (characterTransform == null) {
                SetCurrentDice(null);
                return;
            }

            var position = characterTransform.position;
            SetCurrentDice(null);
            floorWorldPosition = new Vector2(position.x, position.z);
        }

        void EnsureCharacterInstance() {
            if (characterTransform != null) {
                return;
            }

            if (characterObject != null) {
                var instance = Instantiate(characterObject, transform);
                instance.name = "CharacterVisual";
                characterTransform = instance.transform;
                return;
            }

            characterTransform = transform;
        }

        void UpdateCharacterWorldPosition() {
            if (characterTransform == null || board == null) {
                return;
            }

            var worldY = GetCurrentSurfaceWorldY() + characterHeightOffset;

            if (IsOnFloor) {
                characterTransform.position = new Vector3(
                    floorWorldPosition.x,
                    worldY,
                    floorWorldPosition.y);
            } else {
                var diceTransform = currentDice.View.DiceTransform;
                if (diceTransform == null) {
                    return;
                }

                var dicePosition = diceTransform.position;
                characterTransform.position = new Vector3(
                    dicePosition.x + facePosition.x,
                    worldY,
                    dicePosition.z + facePosition.y);
            }

            characterTransform.rotation = Quaternion.identity;
        }

        float GetCurrentSurfaceWorldY() {
            if (IsOnFloor) {
                return board.FloorSurfaceWorldY;
            }

            return currentDice.GetTopSurfaceWorldY();
        }

        bool CanStepBetween(float fromSurfaceY, float toSurfaceY) {
            return Mathf.Abs(fromSurfaceY - toSurfaceY) <= maxStepHeight;
        }

        bool TryTransferToAdjacentDiceAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
                return false;
            }

            var neighbor = registry.GetNeighbor(currentDice, direction);
            if (neighbor == null) {
                return false;
            }

            if (!CanStepBetween(currentDice.GetTopSurfaceWorldY(), neighbor.GetTopSurfaceWorldY())) {
                return false;
            }

            facePosition = RemapFacePositionForTransfer(nextPosition, edgeLimit, direction);
            SetCurrentDice(neighbor);
            return true;
        }

        bool TryTransferToFloorAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
                return false;
            }

            var targetGrid = currentDice.CurrentState.GridPos + direction.ToGridDelta();
            if (!board.IsInside(targetGrid) || registry.TryGetAt(targetGrid, out _)) {
                return false;
            }

            if (!CanStepBetween(currentDice.GetTopSurfaceWorldY(), board.FloorSurfaceWorldY)) {
                return false;
            }

            var diceCenter = currentDice.View.DiceTransform.position;
            var worldPosition = GetWorldPositionAtDiceEdge(diceCenter, nextPosition, edgeLimit, direction);
            SetCurrentDice(null);
            floorWorldPosition = new Vector2(worldPosition.x, worldPosition.z);
            return true;
        }

        bool TryRollAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
                return false;
            }

            if (currentDice.IsDissolving) {
                return false;
            }

            var targetPos = currentDice.CurrentState.GridPos + direction.ToGridDelta();
            if (registry.TryGetAt(targetPos, out _)) {
                return false;
            }

            facePosition = ClampToFace(nextPosition, edgeLimit);

            if (!board.CanDiceRollInto(targetPos)) {
                return false;
            }

            if (!currentDice.TryRoll(direction)) {
                return false;
            }

            return true;
        }

        bool TryStepOntoDiceFromFloor(Vector2 nextPosition, Vector2 move) {
            if (Mathf.Abs(move.x) >= Mathf.Abs(move.y)) {
                if (move.x > 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.East)) {
                    return true;
                }

                if (move.x < 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.West)) {
                    return true;
                }
            } else {
                if (move.y > 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.North)) {
                    return true;
                }

                if (move.y < 0f && TryStepOntoDiceFromFloorDirection(nextPosition, Direction.South)) {
                    return true;
                }
            }

            return false;
        }

        bool TryStepOntoDiceFromFloorDirection(Vector2 nextPosition, Direction direction) {
            var currentGrid = board.WorldToGrid(new Vector3(floorWorldPosition.x, 0f, floorWorldPosition.y));
            var diceGrid = currentGrid + direction.ToGridDelta();

            if (!registry.TryGetAt(diceGrid, out var targetDice)) {
                return false;
            }

            var diceCenter = board.GridToWorld(diceGrid);
            var edgeLimit = GetEdgeLimit();
            var offset = WorldOffsetFromDiceCenter(diceCenter, nextPosition);

            var crossed = direction switch {
                Direction.East => offset.x >= -edgeLimit - EdgeEpsilon,
                Direction.West => offset.x <= edgeLimit + EdgeEpsilon,
                Direction.North => offset.y >= -edgeLimit - EdgeEpsilon,
                Direction.South => offset.y <= edgeLimit + EdgeEpsilon,
                _ => false
            };

            if (!crossed) {
                return false;
            }

            if (!CanStepBetween(board.FloorSurfaceWorldY, targetDice.GetTopSurfaceWorldY())) {
                floorWorldPosition = ClampFloorPosition(nextPosition);
                return true;
            }

            facePosition = RemapFacePositionForTransfer(offset, edgeLimit, direction.Opposite());
            SetCurrentDice(targetDice);
            return true;
        }

        static Vector2 WorldOffsetFromDiceCenter(Vector3 diceCenter, Vector2 worldPosition) {
            return new Vector2(worldPosition.x - diceCenter.x, worldPosition.y - diceCenter.z);
        }

        static Vector3 GetWorldPositionAtDiceEdge(
            Vector3 diceCenter,
            Vector2 nextPosition,
            float edgeLimit,
            Direction direction) {
            return direction switch {
                Direction.East => new Vector3(
                    diceCenter.x + Mathf.Min(nextPosition.x, edgeLimit),
                    0f,
                    diceCenter.z + nextPosition.y),
                Direction.West => new Vector3(
                    diceCenter.x + Mathf.Max(nextPosition.x, -edgeLimit),
                    0f,
                    diceCenter.z + nextPosition.y),
                Direction.North => new Vector3(
                    diceCenter.x + nextPosition.x,
                    0f,
                    diceCenter.z + Mathf.Min(nextPosition.y, edgeLimit)),
                Direction.South => new Vector3(
                    diceCenter.x + nextPosition.x,
                    0f,
                    diceCenter.z + Mathf.Max(nextPosition.y, -edgeLimit)),
                _ => new Vector3(diceCenter.x + nextPosition.x, 0f, diceCenter.z + nextPosition.y)
            };
        }

        Vector2 ClampFloorPosition(Vector2 position) {
            var minX = 0f;
            var minZ = 0f;
            var maxX = (board.Width - 1) * board.CellSize;
            var maxZ = (board.Height - 1) * board.CellSize;
            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minZ, maxZ));
        }

        static bool TryGetCrossingDirection(Vector2 nextPosition, float edgeLimit, Vector2 move, out Direction direction) {
            direction = default;

            if (move.x > 0f && nextPosition.x > edgeLimit) {
                direction = Direction.East;
                return true;
            }

            if (move.x < 0f && nextPosition.x < -edgeLimit) {
                direction = Direction.West;
                return true;
            }

            if (move.y > 0f && nextPosition.y > edgeLimit) {
                direction = Direction.North;
                return true;
            }

            if (move.y < 0f && nextPosition.y < -edgeLimit) {
                direction = Direction.South;
                return true;
            }

            return false;
        }

        static Vector2 RemapFacePositionForTransfer(Vector2 nextPosition, float edgeLimit, Direction direction) {
            return direction switch {
                Direction.East => new Vector2(
                    -edgeLimit + (nextPosition.x - edgeLimit),
                    Mathf.Clamp(nextPosition.y, -edgeLimit, edgeLimit)),
                Direction.West => new Vector2(
                    edgeLimit + (nextPosition.x + edgeLimit),
                    Mathf.Clamp(nextPosition.y, -edgeLimit, edgeLimit)),
                Direction.North => new Vector2(
                    Mathf.Clamp(nextPosition.x, -edgeLimit, edgeLimit),
                    -edgeLimit + (nextPosition.y - edgeLimit)),
                Direction.South => new Vector2(
                    Mathf.Clamp(nextPosition.x, -edgeLimit, edgeLimit),
                    edgeLimit + (nextPosition.y + edgeLimit)),
                _ => nextPosition
            };
        }

        static Vector2 ClampToFace(Vector2 position, float edgeLimit) {
            return new Vector2(
                Mathf.Clamp(position.x, -edgeLimit, edgeLimit),
                Mathf.Clamp(position.y, -edgeLimit, edgeLimit));
        }

        float GetEdgeLimit() {
            return board.CellSize * 0.5f * faceStepRatio;
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
    }
}
