using System;
using DiceGame.Grid;
using DiceGame.Core;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DiceController : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] DiceView diceView;
        [SerializeField] Vector2Int startGridPos = new(2, 2);
        [SerializeField] DiceOrientation startOrientation = DiceOrientation.Default;

        DiceRegistry registry;
        DiceState currentState;
        bool isRolling;
        bool isDissolving;
        bool isInitialized;

        public bool IsRolling => isRolling || (diceView != null && diceView.IsAnimating && !isDissolving);
        public bool IsDissolving => isDissolving;
        public bool IsBusy => IsRolling || isDissolving;
        public DiceState CurrentState => currentState;
        public DiceView View => diceView;

        public event Action<DiceState> StateChanged;
        public event Action<DiceController> Dissolved;

        void Awake() {
            if (diceView == null) {
                diceView = GetComponent<DiceView>();
            }
        }

        void Start() {
            if (!isInitialized && board != null && diceView != null && registry != null) {
                Initialize(startGridPos, startOrientation);
            }
        }

        public void Configure(
            Board targetBoard,
            DiceView view,
            DiceRegistry targetRegistry,
            Vector2Int gridPos,
            DiceOrientation orientation) {
            board = targetBoard;
            diceView = view;
            registry = targetRegistry;
            startGridPos = gridPos;
            startOrientation = orientation;
            Initialize(gridPos, orientation);
        }

        public void Initialize(Vector2Int gridPos, DiceOrientation orientation) {
            isInitialized = true;
            currentState = new DiceState(gridPos, orientation);
            board.RegisterDice(gridPos);
            registry?.Register(this);

            diceView.SnapTo(currentState, board);
            StateChanged?.Invoke(currentState);
        }

        public float GetTopSurfaceWorldY() {
            return diceView != null && board != null
                ? diceView.GetTopSurfaceWorldY(board)
                : board != null ? board.FloorSurfaceWorldY : 0f;
        }

        public bool TryRoll(Direction direction) {
            if (IsBusy || board == null || diceView == null) {
                return false;
            }

            if (!RollResolver.TryRoll(currentState, direction, board, out var nextState)) {
                return false;
            }

            isRolling = true;
            var fromState = currentState;
            currentState = nextState;
            board.MoveDice(fromState.GridPos, nextState.GridPos);
            registry?.MoveDice(this, fromState.GridPos, nextState.GridPos);

            diceView.PlayRoll(direction, fromState, nextState, board, () => {
                isRolling = false;
                StateChanged?.Invoke(currentState);
            });

            return true;
        }

        public void BeginDissolve(Action onComplete) {
            if (isDissolving || board == null || diceView == null) {
                return;
            }

            isDissolving = true;
            diceView.PlayDissolve(board, currentState.Orientation.Top, () => {
                var gridPos = currentState.GridPos;
                board.UnregisterDice(gridPos);
                registry?.Unregister(this);
                Dissolved?.Invoke(this);
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
