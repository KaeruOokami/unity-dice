using System;
using DiceGame.Grid;
using DiceGame.Placement;
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
        public float GroundRollProgress => diceView != null ? diceView.GroundRollProgress : 0f;

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

        public float GetLogicalTopSurfaceWorldY() {
            return diceView != null && board != null
                ? diceView.GetLogicalTopSurfaceWorldY(board)
                : board != null ? board.FloorSurfaceWorldY : 0f;
        }

        public bool TryExecuteSlidePlan(DiceSlidePlan plan) {
            if (IsBusy || isDissolving || board == null || diceView == null || registry == null) {
                return false;
            }

            return BeginSlide(plan.From, plan.To);
        }

        public bool TryExecuteGroundMovePlan(DiceGridMovePlan plan) {
            if (!TryExecuteMovePlan(plan, DiceMoveVisualContext.Ground)) {
                Debug.LogError(
                    $"DiceController: ground move plan execution failed kind={plan.Kind} " +
                    $"from={plan.From.GridPos} to={plan.To.GridPos}");
                return false;
            }

            return true;
        }

        public bool TryExecuteJumpMovePlan(
            DiceGridMovePlan plan,
            float jumpYOffset,
            Func<VerticalMotionState> jumpMotionProvider = null) {
            if (!TryExecuteMovePlan(plan, DiceMoveVisualContext.Jump(jumpYOffset, jumpMotionProvider))) {
                Debug.LogError(
                    $"DiceController: jump move plan execution failed kind={plan.Kind} " +
                    $"from={plan.From.GridPos} to={plan.To.GridPos}");
                return false;
            }

            return true;
        }

        public bool TryInterruptActiveRoll() {
            return TryInterruptActiveRoll(out _);
        }

        public bool TryInterruptActiveRoll(out DiceRollVisualSnapshot snapshot) {
            snapshot = DiceRollVisualSnapshot.Invalid;
            if (!isRolling && (diceView == null || !diceView.IsAnimating)) {
                return false;
            }

            diceView?.TryInterruptRollAnimation(out snapshot);
            isRolling = false;
            return snapshot.IsValid;
        }

        public bool RollbackLogicalStateOnly(DiceState targetState) {
            if (board == null || registry == null) {
                return false;
            }

            var fromState = currentState;
            if (fromState.GridPos == targetState.GridPos
                && fromState.Tier == targetState.Tier
                && fromState.Orientation.Equals(targetState.Orientation)) {
                return true;
            }

            currentState = targetState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                targetState.GridPos,
                fromState.Tier,
                targetState.Tier);
            StateChanged?.Invoke(currentState);
            return true;
        }

        public bool TryRollbackToState(DiceState targetState) {
            if (board == null || diceView == null || registry == null) {
                return false;
            }

            TryInterruptActiveRoll();
            var fromState = currentState;
            if (fromState.GridPos == targetState.GridPos
                && fromState.Tier == targetState.Tier
                && fromState.Orientation.Equals(targetState.Orientation)) {
                diceView.SnapTo(targetState, board, registry);
                return true;
            }

            if (!RollbackLogicalStateOnly(targetState)) {
                return false;
            }

            diceView.SnapTo(targetState, board, registry);
            return true;
        }

        public bool TryExecuteCancelReverseGroundMovePlan(
            DiceGridMovePlan plan,
            DiceRollVisualSnapshot snapshot,
            float cancelProgress) {
            if (isDissolving || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (!snapshot.IsValid) {
                return false;
            }

            ApplyLogicalMove(plan.From, plan.To);
            isRolling = true;
            diceView.PlayCancelGroundRollVisual(
                snapshot,
                plan.To,
                cancelProgress,
                board,
                registry,
                () => {
                    isRolling = false;
                    StateChanged?.Invoke(currentState);
                });

            return true;
        }

        public bool TryExecuteCancelJumpMovePlan(
            DiceGridMovePlan plan,
            DiceRollVisualSnapshot snapshot,
            Func<VerticalMotionState> jumpMotionProvider) {
            if (isDissolving || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (!snapshot.IsValid || jumpMotionProvider == null) {
                return false;
            }

            ApplyLogicalMove(plan.From, plan.To);
            isRolling = true;
            diceView.PlayCancelJumpParallelRollVisual(
                snapshot,
                plan,
                board,
                registry,
                () => {
                    isRolling = false;
                    StateChanged?.Invoke(currentState);
                },
                jumpMotionProvider);

            return true;
        }

        void ApplyLogicalMove(DiceState fromState, DiceState toState) {
            currentState = toState;
            registry.MoveDice(
                this,
                fromState.GridPos,
                toState.GridPos,
                fromState.Tier,
                toState.Tier);
        }

        bool TryExecuteMovePlan(DiceGridMovePlan plan, DiceMoveVisualContext context) {
            if (isDissolving || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            ApplyLogicalMove(plan.From, plan.To);
            isRolling = true;
            PlayVisualForPlan(
                plan,
                context,
                () => {
                    isRolling = false;
                    StateChanged?.Invoke(currentState);
                });

            return true;
        }

        void PlayVisualForPlan(DiceGridMovePlan plan, DiceMoveVisualContext context, Action onComplete) {
            switch (plan.Kind) {
                case DiceGridMoveKind.Parallel:
                    if (context.IsJump) {
                        PlayJumpParallelRollVisual(plan, context, onComplete);
                    } else {
                        PlayGroundParallelRollVisual(plan, onComplete);
                    }

                    return;
                case DiceGridMoveKind.Demote:
                    if (context.IsJump) {
                        PlayJumpTierChangeRollVisual(plan, context, onComplete);
                    } else {
                        PlayGroundTierChangeRollVisual(plan, onComplete);
                    }

                    return;
                case DiceGridMoveKind.Stack:
                    if (context.IsJump) {
                        PlayJumpTierChangeRollVisual(plan, context, onComplete);
                    } else {
                        PlayGroundTierChangeRollVisual(plan, onComplete);
                    }

                    return;
            }

            onComplete?.Invoke();
        }

        void PlayGroundParallelRollVisual(DiceGridMovePlan plan, Action onComplete) {
            diceView.PlayJumpRoll(
                plan.Direction,
                plan.From,
                plan.To,
                0f,
                plan.Distance,
                board,
                registry,
                onComplete,
                fallBeforeSnap: false);
        }

        void PlayJumpParallelRollVisual(
            DiceGridMovePlan plan,
            DiceMoveVisualContext context,
            Action onComplete) {
            diceView.PlayJumpRoll(
                plan.Direction,
                plan.From,
                plan.To,
                context.JumpYOffset,
                plan.Distance,
                board,
                registry,
                onComplete,
                fallBeforeSnap: false,
                context.JumpMotionProvider);
        }

        void PlayGroundTierChangeRollVisual(DiceGridMovePlan plan, Action onComplete) {
            var transition = plan.Kind == DiceGridMoveKind.Stack
                ? DiceTransition.RollThenRise(plan.From, plan.To, plan.Direction)
                : DiceTransition.RollThenDemote(plan.From, plan.To, plan.Direction);
            diceView.PlayTransition(transition, board, registry, onComplete);
        }

        void PlayJumpTierChangeRollVisual(
            DiceGridMovePlan plan,
            DiceMoveVisualContext context,
            Action onComplete) {
            diceView.PlayJumpRoll(
                plan.Direction,
                plan.From,
                plan.To,
                context.JumpYOffset,
                plan.Distance,
                board,
                registry,
                onComplete,
                fallBeforeSnap: context.JumpMotionProvider == null,
                context.JumpMotionProvider);
        }

        void PlaySlideVisual(DiceState fromState, DiceState toState, Action onComplete) {
            var transition = DiceTransition.GridMove(fromState, toState);
            diceView.PlayTransition(transition, board, registry, onComplete);
        }

        bool BeginSlide(DiceState fromState, DiceState nextState) {
            ApplyLogicalMove(fromState, nextState);
            isRolling = true;
            PlaySlideVisual(fromState, nextState, () => {
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
            ApplyLogicalMove(fromState, toState);
            isRolling = true;

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
