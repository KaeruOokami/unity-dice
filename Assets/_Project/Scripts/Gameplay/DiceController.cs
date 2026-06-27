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
        public bool IsDissolveGhost =>
            isDissolving && diceView != null && diceView.IsDissolveGhost;
        public bool IsCarried => isCarried;
        public bool IsBusy => IsRolling || isDissolving || isCarried;
        public DiceState CurrentState => currentState;
        public DiceView View => diceView;

        public event Action<DiceState> StateChanged;
        public event Action<DiceController> Dissolved;
        public event Action<DiceController> BecameDissolveGhost;

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

            return BeginGridTransition(fromState: currentState, nextState);
        }

        bool TrySlideTop(Direction direction) {
            if (!SlideResolver.TrySlideTop(currentState, direction, registry, out var nextState, out var result)) {
                return false;
            }

            if (result == TopSlideResult.FallToBottom) {
                return TryRollThenDemote(direction);
            }

            return BeginGridTransition(currentState, nextState);
        }

        public bool TryJumpRoll(Direction direction, float jumpYOffset) {
            if (isDissolving || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (currentState.Tier != DiceStackTier.Bottom) {
                return false;
            }

            var hasTopOnSameCell = registry.HasTopAt(currentState.GridPos);
            if (!RollResolver.TryRoll(currentState, direction, registry, hasTopOnSameCell, out var nextState)) {
                return false;
            }

            var fromState = currentState;
            isRolling = true;
            currentState = nextState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                nextState.GridPos,
                fromState.Tier,
                nextState.Tier);

            diceView.PlayJumpRoll(direction, fromState, nextState, jumpYOffset, board, registry, () => {
                isRolling = false;
                StateChanged?.Invoke(currentState);
            });

            return true;
        }

        public bool TryJumpStack(Direction direction, float jumpYOffset) {
            if (isDissolving || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (currentState.Tier != DiceStackTier.Bottom) {
                return false;
            }

            var targetPos = currentState.GridPos + direction.ToGridDelta();
            if (!registry.CanPlaceTopDiceAt(targetPos)) {
                return false;
            }

            var rolledOrientation = currentState.Orientation.Roll(direction);
            if (!rolledOrientation.IsValid()) {
                return false;
            }

            var nextState = new DiceState(targetPos, rolledOrientation, DiceStackTier.Top);
            var fromState = currentState;
            isRolling = true;
            currentState = nextState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                nextState.GridPos,
                DiceStackTier.Bottom,
                DiceStackTier.Top);

            diceView.PlayJumpRoll(direction, fromState, nextState, jumpYOffset, board, registry, () => {
                isRolling = false;
                StateChanged?.Invoke(currentState);
            });

            return true;
        }

        public bool TryJumpRollThenDemote(Direction direction, float jumpYOffset) {
            if (isDissolving || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (currentState.Tier != DiceStackTier.Top) {
                return false;
            }

            if (!SlideResolver.TrySlideTop(currentState, direction, registry, out var nextState, out var result)
                || result != TopSlideResult.FallToBottom) {
                return false;
            }

            var rolledOrientation = currentState.Orientation.Roll(direction);
            if (!rolledOrientation.IsValid()) {
                return false;
            }

            nextState = new DiceState(nextState.GridPos, rolledOrientation, DiceStackTier.Bottom);

            var fromState = currentState;
            isRolling = true;
            currentState = nextState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                nextState.GridPos,
                DiceStackTier.Top,
                DiceStackTier.Bottom);

            diceView.PlayJumpRoll(
                direction,
                fromState,
                nextState,
                jumpYOffset,
                board,
                registry,
                () => {
                    isRolling = false;
                    StateChanged?.Invoke(currentState);
                },
                fallBeforeSnap: true);

            return true;
        }

        public bool TryRollThenDemote(Direction direction) {
            if (IsBusy || isDissolving || board == null || diceView == null || registry == null) {
                return false;
            }

            if (currentState.Tier != DiceStackTier.Top) {
                return false;
            }

            if (!SlideResolver.TrySlideTop(currentState, direction, registry, out var nextState, out var result)
                || result != TopSlideResult.FallToBottom) {
                return false;
            }

            var rolledOrientation = currentState.Orientation.Roll(direction);
            if (!rolledOrientation.IsValid()) {
                return false;
            }

            nextState = new DiceState(nextState.GridPos, rolledOrientation, DiceStackTier.Bottom);

            var fromState = currentState;
            isRolling = true;
            currentState = nextState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                nextState.GridPos,
                DiceStackTier.Top,
                DiceStackTier.Bottom);

            diceView.PlayJumpRoll(
                direction,
                fromState,
                nextState,
                0f,
                board,
                registry,
                () => {
                    isRolling = false;
                    StateChanged?.Invoke(currentState);
                },
                fallBeforeSnap: true);

            return true;
        }

        bool BeginGridTransition(DiceState fromState, DiceState nextState) {
            isRolling = true;
            currentState = nextState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                nextState.GridPos,
                fromState.Tier,
                nextState.Tier);

            var transition = DiceTransition.GridMove(fromState, nextState);
            diceView.PlayTransition(transition, board, registry, () => {
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

        public void OnBecameDissolveGhost() {
            if (!IsDissolveGhost) {
                return;
            }

            var grid = currentState.GridPos;
            registry?.RemoveFromGrid(this);

            if (registry != null
                && registry.TryGetTopAt(grid, out var top)
                && top != null
                && top != this) {
                top.CrushDissolvingBottomAndDemote(this);
            }

            BecameDissolveGhost?.Invoke(this);
        }

        public void CrushDissolvingBottomAndDemote(DiceController ghostBottom) {
            if (isCarried
                || isDissolving
                || board == null
                || diceView == null
                || registry == null
                || currentState.Tier != DiceStackTier.Top) {
                return;
            }

            if (ghostBottom == null || !ghostBottom.IsDissolveGhost) {
                return;
            }

            var fromWorld = diceView.DiceTransform.position;
            ghostBottom.CompleteDissolveFromCrush();

            var fromState = currentState;
            var toState = new DiceState(fromState.GridPos, fromState.Orientation, DiceStackTier.Bottom);
            currentState = toState;
            isRolling = true;
            registry.MoveDice(this, fromState.GridPos, toState.GridPos, DiceStackTier.Top, DiceStackTier.Bottom);

            var transition = DiceTransition.CrushDemote(fromState, toState, fromWorld);
            diceView.PlayTransition(transition, board, registry, () => {
                isRolling = false;
                StateChanged?.Invoke(currentState);
            });
        }

        public void NotifyStackedTopSync() {
            registry?.SyncStackedTopAt(currentState.GridPos, board);
        }

        public void OnCeasedDissolveGhost() {
            if (IsDissolveGhost || !isDissolving) {
                return;
            }

            registry?.RestoreToGrid(this);
        }

        public void CompleteDissolveFromCrush() {
            if (!isDissolving) {
                return;
            }

            diceView?.CancelDissolve();
            isDissolving = false;
            registry?.Unregister(this);
            Dissolved?.Invoke(this);
            Destroy(gameObject);
        }

        public bool TryBeginCarry(Vector3 carryWorldTarget, Action onComplete) {
            if (IsBusy || board == null || diceView == null || diceView.DiceTransform == null) {
                return false;
            }

            isCarried = true;
            registry?.Unregister(this);

            var fromWorld = diceView.DiceTransform.position;
            var transition = DiceTransition.FreeMove(fromWorld, carryWorldTarget, snapToGridOnComplete: false);
            diceView.PlayTransition(transition, board, registry, () => {
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
            var transition = DiceTransition.FreeMove(fromWorld, toWorld, snapToGridOnComplete: true, toState);

            diceView.PlayTransition(transition, board, registry, () => {
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
