using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class CharacterController : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] DiceController diceController;
        [SerializeField] DiceView diceView;
        [SerializeField] GameObject characterObject;
        [SerializeField] float characterHeightOffset = 0.15f;
        [SerializeField] float faceStepRatio = 0.85f;
        [SerializeField] float maxMoveSpeed = 2.5f;
        [SerializeField] float moveAcceleration = 10f;
        [SerializeField] float rollCenterPullSpeed = 2.5f;

        Transform characterTransform;
        Vector2 facePosition;
        float currentSpeed;
        bool isInitialized;

        public bool IsBusy => diceController != null && diceController.IsBusy;
        public Vector2 FacePosition => facePosition;

        void Awake() {
            if (diceController == null) {
                diceController = GetComponent<DiceController>();
            }

            if (diceView == null) {
                diceView = GetComponent<DiceView>();
            }
        }

        void OnEnable() {
            if (diceController != null) {
                diceController.StateChanged += OnDiceStateChanged;
            }
        }

        void OnDisable() {
            if (diceController != null) {
                diceController.StateChanged -= OnDiceStateChanged;
            }
        }

        public void Configure(Board targetBoard, DiceController controller, DiceView view) {
            board = targetBoard;
            diceController = controller;
            diceView = view;
            Initialize();
        }

        public void Initialize() {
            if (board == null || diceController == null || diceView == null) {
                Debug.LogError("CharacterController: Board, DiceController, or DiceView is not assigned.");
                return;
            }

            if (characterObject == null) {
                Debug.LogError("CharacterController: characterObject is not assigned.");
                return;
            }

            diceView.EnsureDiceInstance();
            if (diceView.DiceTransform == null) {
                Debug.LogError("CharacterController: Dice visual is not available.");
                return;
            }

            EnsureCharacterInstance();
            facePosition = Vector2.zero;
            currentSpeed = 0f;
            isInitialized = true;
            UpdateCharacterWorldPosition();
        }

        void Update() {
            if (!isInitialized) {
                return;
            }

            var input = GetInputDirection();
            var isRolling = diceController != null && diceController.IsBusy;

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

        void EnsureCharacterInstance() {
            if (characterTransform != null) {
                characterTransform.SetParent(transform, false);
                return;
            }

            var instance = Instantiate(characterObject, transform);
            instance.name = "Character";
            characterTransform = instance.transform;
        }

        void UpdateCharacterWorldPosition() {
            if (characterTransform == null || board == null || diceView.DiceTransform == null) {
                return;
            }

            var dicePosition = diceView.DiceTransform.position;
            var half = board.CellSize * 0.5f;
            var worldY = dicePosition.y + half + characterHeightOffset;

            characterTransform.position = new Vector3(
                dicePosition.x + facePosition.x,
                worldY,
                dicePosition.z + facePosition.y);
            characterTransform.rotation = Quaternion.identity;
        }

        bool TryRollAtEdge(Vector2 nextPosition, float edgeLimit, Vector2 move) {
            Direction? direction = null;

            if (move.x > 0f && nextPosition.x > edgeLimit) {
                direction = Direction.East;
            } else if (move.x < 0f && nextPosition.x < -edgeLimit) {
                direction = Direction.West;
            } else if (move.y > 0f && nextPosition.y > edgeLimit) {
                direction = Direction.North;
            } else if (move.y < 0f && nextPosition.y < -edgeLimit) {
                direction = Direction.South;
            }

            if (!direction.HasValue) {
                return false;
            }

            facePosition = ClampToFace(nextPosition, edgeLimit);

            if (!diceController.TryRoll(direction.Value)) {
                return false;
            }

            return true;
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
