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
        [SerializeField] DiceStackTier startTier = DiceStackTier.Bottom;

        DiceRegistry registry;
        DiceState currentState;
        bool isRolling;
        bool isDissolving;
        bool isCarried;
        bool isInitialized;

        public bool IsRolling => isRolling || (diceView != null && diceView.IsAnimating && !isDissolving && !isCarried);
        public bool IsDissolving => isDissolving;
        public bool IsCarried => isCarried;
        public bool IsBusy => IsRolling || isDissolving || isCarried;
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
                Initialize(startGridPos, startOrientation, startTier);
            }
        }

        public void Configure(
            Board targetBoard,
            DiceView view,
            DiceRegistry targetRegistry,
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceStackTier tier = DiceStackTier.Bottom) {
            board = targetBoard;
            diceView = view;
            registry = targetRegistry;
            startGridPos = gridPos;
            startOrientation = orientation;
            startTier = tier;
            Initialize(gridPos, orientation, tier);
        }

        public void Initialize(Vector2Int gridPos, DiceOrientation orientation, DiceStackTier tier = DiceStackTier.Bottom) {
            isInitialized = true;
            currentState = new DiceState(gridPos, orientation, tier);
            registry?.Place(this, gridPos, tier);

            diceView.SnapTo(currentState, board, registry);
            ConfigurePushBody();
            StateChanged?.Invoke(currentState);
        }

        void ConfigurePushBody() {
            var pushBody = GetComponentInChildren<DicePushBody>();
            pushBody?.Configure(board);
        }

        public float GetTopSurfaceWorldY() {
            return diceView != null && board != null
                ? diceView.GetTopSurfaceWorldY(board)
                : board != null ? board.FloorSurfaceWorldY : 0f;
        }

        public bool TryRoll(Direction direction) {
            if (IsBusy || board == null || diceView == null || registry == null) {
                return false;
            }

            var hasTopOnSameCell = registry.HasTopAt(currentState.GridPos);
            if (!RollResolver.TryRoll(currentState, direction, registry, hasTopOnSameCell, out var nextState)) {
                return false;
            }

            isRolling = true;
            var fromState = currentState;
            currentState = nextState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                nextState.GridPos,
                fromState.Tier,
                nextState.Tier);

            diceView.PlayRoll(direction, fromState, nextState, board, registry, () => {
                isRolling = false;
                StateChanged?.Invoke(currentState);
            });

            return true;
        }

        public bool TrySlide(Direction direction) {
            if (IsBusy || isDissolving || board == null || diceView == null || registry == null) {
                return false;
            }

            if (currentState.Tier == DiceStackTier.Top) {
                return TrySlideTop(direction);
            }

            if (!SlideResolver.TrySlideBottom(currentState, direction, registry, out var nextState)) {
                return false;
            }

            return BeginSlide(nextState);
        }

        bool TrySlideTop(Direction direction) {
            if (!SlideResolver.TrySlideTop(currentState, direction, registry, out var nextState, out var result)) {
                return false;
            }

            isRolling = true;
            var fromState = currentState;
            currentState = nextState;

            var fromTier = fromState.Tier;
            var toTier = result == TopSlideResult.Parallel ? DiceStackTier.Top : DiceStackTier.Bottom;
            registry.MoveDice(this, fromState.GridPos, nextState.GridPos, fromTier, toTier);

            Action onComplete = () => {
                isRolling = false;
                StateChanged?.Invoke(currentState);
            };

            if (result == TopSlideResult.FallToBottom) {
                diceView.PlayStackMoveFallToBottom(fromState, nextState, board, registry, onComplete);
            } else {
                diceView.PlayStackMove(fromState, nextState, board, registry, onComplete);
            }

            return true;
        }

        bool BeginSlide(DiceState nextState) {
            isRolling = true;
            var fromState = currentState;
            currentState = nextState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                nextState.GridPos,
                fromState.Tier,
                nextState.Tier);

            diceView.PlaySlide(fromState, nextState, board, registry, () => {
                isRolling = false;
                StateChanged?.Invoke(currentState);
            });

            return true;
        }

        public void BeginDissolve(Action onComplete) {
            if (isDissolving || isCarried || board == null || diceView == null) {
                return;
            }

            isDissolving = true;
            diceView.PlayDissolve(board, currentState.Orientation.Top, () => {
                registry?.Unregister(this);
                Dissolved?.Invoke(this);
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }

        public void RetreatDissolve(float amount) {
            if (!isDissolving || diceView == null) {
                return;
            }

            diceView.RetreatDissolve(amount);
        }

        public bool TryBeginCarry(Vector3 carryWorldTarget, Action onComplete) {
            if (IsBusy || board == null || diceView == null || diceView.DiceTransform == null) {
                return false;
            }

            isCarried = true;
            registry?.Unregister(this);

            var fromWorld = diceView.DiceTransform.position;
            diceView.PlayLift(fromWorld, carryWorldTarget, () => {
                onComplete?.Invoke();
            });

            return true;
        }

        public bool TryPlaceAt(Vector2Int targetGrid, DiceStackTier targetTier, Vector3 fromWorld, Action onComplete) {
            if (!isCarried || board == null || diceView == null || registry == null) {
                return false;
            }

            if (targetTier == DiceStackTier.Top) {
                if (!registry.CanPlaceTopDiceAt(targetGrid)) {
                    return false;
                }
            } else if (!registry.CanPlaceBottomDiceAt(targetGrid)) {
                return false;
            }

            var toState = new DiceState(targetGrid, currentState.Orientation, targetTier);
            var toWorld = diceView.GetAnchoredWorldPosition(toState, board, registry);

            diceView.PlayPlace(fromWorld, toWorld, toState, board, registry, () => {
                currentState = toState;
                isCarried = false;
                registry.Place(this, targetGrid, targetTier);
                ConfigurePushBody();
                StateChanged?.Invoke(currentState);
                onComplete?.Invoke();
            });

            return true;
        }
    }
}
