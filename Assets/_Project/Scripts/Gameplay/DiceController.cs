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
        bool isBusy;
        bool isInitialized;

        public bool IsBusy => isBusy || (diceView != null && diceView.IsAnimating);
        public DiceState CurrentState => currentState;
        public DiceView View => diceView;

        public event Action<DiceState> StateChanged;

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

        public bool TryRoll(Direction direction) {
            if (IsBusy || board == null || diceView == null) {
                return false;
            }

            if (!RollResolver.TryRoll(currentState, direction, board, out var nextState)) {
                return false;
            }

            isBusy = true;
            var fromState = currentState;
            currentState = nextState;
            board.MoveDice(fromState.GridPos, nextState.GridPos);
            registry?.MoveDice(this, fromState.GridPos, nextState.GridPos);

            diceView.PlayRoll(direction, fromState, nextState, board, () => {
                isBusy = false;
                StateChanged?.Invoke(currentState);
            });

            return true;
        }
    }
}
