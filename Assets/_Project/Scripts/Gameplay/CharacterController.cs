using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class CharacterController : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] GameObject characterObject;
        [SerializeField] float characterHeightOffset = 0.15f;
        [SerializeField] float faceStepRatio = 0.85f;
        [SerializeField] float maxMoveSpeed = 2.5f;
        [SerializeField] float moveAcceleration = 10f;
        [SerializeField] float rollCenterPullSpeed = 2.5f;

        DiceRegistry registry;
        DiceController currentDice;
        Transform characterTransform;
        Vector2 facePosition;
        float currentSpeed;
        bool isInitialized;

        public bool IsBusy => currentDice != null && currentDice.IsBusy;
        public Vector2 FacePosition => facePosition;
        public DiceController CurrentDice => currentDice;

        public void Configure(Board targetBoard, DiceRegistry targetRegistry, DiceController startDice) {
            board = targetBoard;
            registry = targetRegistry;
            SetCurrentDice(startDice);
            Initialize();
        }

        public void Initialize() {
            if (board == null || registry == null || currentDice == null) {
                Debug.LogError("CharacterController: Board, DiceRegistry, or start Dice is not assigned.");
                return;
            }

            if (currentDice.View == null) {
                Debug.LogError("CharacterController: Current dice has no DiceView.");
                return;
            }

            currentDice.View.EnsureDiceInstance();
            if (currentDice.View.DiceTransform == null) {
                Debug.LogError("CharacterController: Dice visual is not available.");
                return;
            }

            EnsureCharacterInstance();
            facePosition = Vector2.zero;
            currentSpeed = 0f;
            isInitialized = true;
            UpdateCharacterWorldPosition();
        }

        void OnDisable() {
            UnsubscribeCurrentDice();
        }

        void Update() {
            if (!isInitialized) {
                return;
            }

            var input = GetInputDirection();
            var isRolling = currentDice != null && currentDice.IsBusy;

            if (isRolling) {
                UpdateDuringRoll(input);
            } else {
                UpdateNormal(input);
            }
        }

        void LateUpdate() {
            if (!isInitialized) {
                return;
            }

            UpdateCharacterWorldPosition();
        }

        void UpdateNormal(Vector2 input) {
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

            if (TryTransferAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            if (TryRollAtEdge(nextPosition, edgeLimit, move)) {
                return;
            }

            facePosition = ClampToFace(nextPosition, edgeLimit);
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
            if (characterTransform == null || board == null || currentDice == null) {
                return;
            }

            var diceTransform = currentDice.View.DiceTransform;
            if (diceTransform == null) {
                return;
            }

            var dicePosition = diceTransform.position;
            var half = board.CellSize * 0.5f;
            var worldY = dicePosition.y + half + characterHeightOffset;

            characterTransform.position = new Vector3(
                dicePosition.x + facePosition.x,
                worldY,
                dicePosition.z + facePosition.y);
            characterTransform.rotation = Quaternion.identity;
        }

        bool TryTransferAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
                return false;
            }

            var neighbor = registry.GetNeighbor(currentDice, direction);
            if (neighbor == null) {
                return false;
            }

            facePosition = RemapFacePositionForTransfer(nextPosition, edgeLimit, direction);
            SetCurrentDice(neighbor);
            return true;
        }

        bool TryRollAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            if (!TryGetCrossingDirection(nextPosition, edgeLimit, move, out var direction)) {
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
