using System;
using DiceGame.Config;
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
        [SerializeField] DiceKind startKind = DiceKind.Normal;

        DiceRegistry registry;
        PlayerMatchActionContext matchActionContext;
        ITierFallMatchNotifier tierFallMatchNotifier;
        DiceState currentState;
        bool isRolling;
        bool isSpawning;
        ErasureKind erasureKind = ErasureKind.None;
        bool isVanishing;
        bool isCarried;
        bool isInitialized;

        public bool IsSpawning => isSpawning;
        public bool IsRolling =>
            !isSpawning
            && (isRolling || (diceView != null && diceView.IsAnimating && !IsErasing && !isVanishing && !isCarried));
        /// <summary>
        /// True while the dice visual is moving in a way the standing player should follow
        /// (spawn appear / roll / slide), including spawn where <see cref="IsRolling"/> is false.
        /// </summary>
        public bool IsMotionFollowActive => IsSpawning || IsRolling;
        public bool IsErasing => erasureKind != ErasureKind.None;
        public bool IsSinkErasing => erasureKind == ErasureKind.Sink;
        public bool IsRadianceErasing => erasureKind == ErasureKind.Radiance;
        public ErasureKind ErasureKind => erasureKind;
        public bool IsVanishing => isVanishing;
        public bool IsErasureGhost =>
            IsSinkErasing && diceView != null && diceView.IsErasureGhost;
        public bool IsCarried => isCarried;
        public bool IsBusy => IsRolling || isSpawning || IsErasing || isCarried;
        public DiceState CurrentState => currentState;
        public DiceKind Kind => currentState.Kind;
        public DiceCapabilities Capabilities => DiceBehaviorResolver.GetCapabilities(Kind);
        public bool IsPlayerMovable => registry != null && IronAdjacencyBlock.IsPlayerMovable(this, registry);
        public bool CanJumpCoupleWithPlayer =>
            registry != null && IronAdjacencyBlock.CanJumpCoupleWithPlayer(this, registry);
        public DiceView View => diceView;
        public float GroundRollProgress => diceView != null ? diceView.GroundRollProgress : 0f;

        public event Action<DiceState> StateChanged;
        public event Action<DiceController> Erased;
        public event Action<DiceController> ErasureStarted;
        public event Action<DiceController> BecameErasureGhost;

        void Awake() {
            if (diceView == null) {
                diceView = GetComponent<DiceView>();
            }
        }

        void Start() {
            if (!isInitialized && board != null && diceView != null && registry != null) {
                Initialize(startGridPos, startOrientation, startTier, startKind);
            }
        }

        public void Configure(
            Board targetBoard,
            DiceView view,
            DiceRegistry targetRegistry,
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceStackTier tier = DiceStackTier.Bottom,
            DiceKind kind = DiceKind.Normal) {
            board = targetBoard;
            diceView = view;
            registry = targetRegistry;
            startGridPos = gridPos;
            startOrientation = orientation;
            startTier = tier;
            startKind = kind;
            Initialize(gridPos, orientation, tier, kind);
        }

        public void ConfigureMatchActionContext(PlayerMatchActionContext actionContext) {
            matchActionContext = actionContext;
        }

        public void ConfigureTierFallMatchNotifier(ITierFallMatchNotifier notifier) {
            tierFallMatchNotifier = notifier;
        }

        public void Initialize(
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceStackTier tier = DiceStackTier.Bottom,
            DiceKind kind = DiceKind.Normal) {
            isInitialized = true;
            currentState = new DiceState(gridPos, orientation, tier, kind);
            registry?.Place(this, gridPos, tier);

            diceView.SnapTo(currentState, board, registry);
            ConfigurePushBody();
            StateChanged?.Invoke(currentState);
        }

        public void ConfigureWithSpawnAppear(
            Board targetBoard,
            DiceView view,
            DiceRegistry targetRegistry,
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceSpawnSettings spawnSettings,
            Action onComplete = null) {
            if (spawnSettings == null) {
                Debug.LogError("DiceController: DiceSpawnSettings is required for spawn appear.");
                return;
            }

            ConfigureWithSpawnAppear(
                targetBoard,
                view,
                targetRegistry,
                gridPos,
                orientation,
                spawnSettings,
                DiceStackTier.Bottom,
                startKind,
                onComplete);
        }

        public void ConfigureWithSpawnAppear(
            Board targetBoard,
            DiceView view,
            DiceRegistry targetRegistry,
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceSpawnSettings spawnSettings,
            DiceStackTier tier,
            Action onComplete = null) {
            ConfigureWithSpawnAppear(
                targetBoard,
                view,
                targetRegistry,
                gridPos,
                orientation,
                spawnSettings,
                tier,
                startKind,
                onComplete);
        }

        public void ConfigureWithSpawnAppear(
            Board targetBoard,
            DiceView view,
            DiceRegistry targetRegistry,
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceSpawnSettings spawnSettings,
            DiceStackTier tier,
            DiceKind kind,
            Action onComplete = null) {
            if (spawnSettings == null) {
                Debug.LogError("DiceController: DiceSpawnSettings is required for spawn appear.");
                return;
            }

            board = targetBoard;
            diceView = view;
            registry = targetRegistry;
            startGridPos = gridPos;
            startOrientation = orientation;
            startTier = tier;
            startKind = kind;
            BeginSpawnAppear(gridPos, orientation, tier, kind, spawnSettings, false, onComplete);
        }

        public void ConfigureWithSpawnAppear(
            Board targetBoard,
            DiceView view,
            DiceRegistry targetRegistry,
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceSpawnSettings spawnSettings,
            DiceStackTier tier,
            DiceKind kind,
            bool forceFallFromAbove,
            Action onComplete = null) {
            if (spawnSettings == null) {
                Debug.LogError("DiceController: DiceSpawnSettings is required for spawn appear.");
                return;
            }

            board = targetBoard;
            diceView = view;
            registry = targetRegistry;
            startGridPos = gridPos;
            startOrientation = orientation;
            startTier = tier;
            startKind = kind;
            BeginSpawnAppear(gridPos, orientation, tier, kind, spawnSettings, forceFallFromAbove, onComplete);
        }

        void BeginSpawnAppear(
            Vector2Int gridPos,
            DiceOrientation orientation,
            DiceStackTier tier,
            DiceKind kind,
            DiceSpawnSettings spawnSettings,
            bool forceFallFromAbove,
            Action onComplete) {
            isInitialized = true;
            isSpawning = true;
            currentState = new DiceState(gridPos, orientation, tier, kind);
            registry?.Place(this, gridPos, tier);

            void OnSpawnComplete() {
                isSpawning = false;
                ConfigurePushBody();
                StateChanged?.Invoke(currentState);
                onComplete?.Invoke();
            }

            if (forceFallFromAbove || tier == DiceStackTier.Top) {
                var bounceRestitution = Capabilities.HasSpawnBounce
                    ? spawnSettings.BounceRestitution
                    : 0f;
                var maxBounceCount = Capabilities.HasSpawnBounce
                    ? spawnSettings.MaxBounceCount
                    : 0;
                diceView.PlaySpawnAppear(
                    currentState,
                    board,
                    registry,
                    spawnSettings.SpawnHeight,
                    bounceRestitution,
                    maxBounceCount,
                    spawnSettings.MinBounceVelocity,
                    OnSpawnComplete);
            } else {
                diceView.PlayBottomEmergenceAppear(
                    currentState,
                    board,
                    registry,
                    spawnSettings.BottomEmergenceDuration,
                    OnSpawnComplete);
            }
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

        public bool TryExecuteSlidePlan(DiceSlidePlan plan, PlayerSlot actionOwner) {
            if (IsBusy || IsErasing || isVanishing || board == null || diceView == null || registry == null) {
                return false;
            }

            if (Capabilities.HasMagnetCoupling) {
                return MagnetMoveExecutor.TryExecuteSlide(this, plan, registry, matchActionContext, actionOwner);
            }

            return TryExecuteSlidePlanInternal(plan);
        }

        internal bool TryExecuteSlidePlanInternal(DiceSlidePlan plan) {
            if (IsBusy || IsErasing || isVanishing || board == null || diceView == null || registry == null) {
                return false;
            }

            return BeginSlide(plan.From, plan.To);
        }

        public bool TryExecuteGroundMovePlan(DiceGridMovePlan plan, PassabilityContext context) {
            if (IsErasing || isVanishing || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (Capabilities.HasMagnetCoupling) {
                var occupancyQuery = new CellOccupancyQuery(board, registry);
                var gridPlanBuilder = new GridMovePlanBuilder(registry, occupancyQuery);
                if (!MagnetMoveExecutor.TryExecuteGroundRoll(this, plan, registry, gridPlanBuilder, context, matchActionContext)) {
                    return false;
                }

                return true;
            }

            return TryExecuteGroundMovePlanInternal(plan);
        }

        internal bool TryExecuteGroundMovePlanInternal(DiceGridMovePlan plan) {
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
            if (isSpawning || (!isRolling && (diceView == null || !diceView.IsAnimating))) {
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
            if (IsErasing || isCarried || isRolling || board == null || diceView == null || registry == null) {
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
                    NotifyActionMoveCompleted();
                    StateChanged?.Invoke(currentState);
                });

            return true;
        }

        public bool TryExecuteCancelJumpMovePlan(
            DiceGridMovePlan plan,
            DiceRollVisualSnapshot snapshot,
            Func<VerticalMotionState> jumpMotionProvider) {
            if (IsErasing || isCarried || isRolling || board == null || diceView == null || registry == null) {
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
                    NotifyActionMoveCompleted(plan.From, plan.To);
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

        void NotifyActionMoveCompleted(DiceState fromState, DiceState toState) {
            if (PlayerMatchActionContext.IsActionParticipationMove(fromState, toState)) {
                matchActionContext?.NotifyParticipantMoveCompleted();
            }
        }

        void NotifyActionMoveCompleted() {
            matchActionContext?.NotifyParticipantMoveCompleted();
        }

        bool TryExecuteMovePlan(DiceGridMovePlan plan, DiceMoveVisualContext context) {
            if (IsErasing || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            ApplyLogicalMove(plan.From, plan.To);
            isRolling = true;
            PlayVisualForPlan(
                plan,
                context,
                () => {
                    isRolling = false;
                    NotifyActionMoveCompleted(plan.From, plan.To);
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
            var distance = MovementTransitionEvaluator.GetOrthogonalDistance(fromState.GridPos, toState.GridPos);
            diceView.PlayTransition(transition, board, registry, onComplete, Mathf.Max(1, distance));
        }

        bool BeginSlide(DiceState fromState, DiceState nextState) {
            ApplyLogicalMove(fromState, nextState);
            isRolling = true;
            PlaySlideVisual(fromState, nextState, () => {
                isRolling = false;
                NotifyActionMoveCompleted(fromState, nextState);
                StateChanged?.Invoke(currentState);
            });

            return true;
        }

        public void BeginErasure(ErasureKind kind, Action onComplete) {
            BeginErasure(kind, null, onComplete);
        }

        public void BeginErasure(ErasureKind kind, Color? emissionColor, Action onComplete) {
            if (IsErasing || isVanishing || isCarried || board == null || diceView == null || kind == ErasureKind.None) {
                return;
            }

            erasureKind = kind;
            ErasureStarted?.Invoke(this);
            diceView.PlayErasure(kind, board, currentState.Orientation.Top, emissionColor, () => {
                registry?.Unregister(this);
                erasureKind = ErasureKind.None;
                Erased?.Invoke(this);
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }

        public void BeginErasureForCurrentTier(Color? emissionColor, Action onComplete) {
            var kind = currentState.Tier == DiceStackTier.Top
                ? ErasureKind.Radiance
                : ErasureKind.Sink;
            BeginErasure(kind, emissionColor, onComplete);
        }

        public void SetErasureEmissionColor(Color emissionColor) {
            diceView?.SetErasureEmissionColor(emissionColor);
        }

        public void RetreatErasure(float amount) {
            if (!IsErasing || diceView == null) {
                return;
            }

            diceView.RetreatErasure(amount);
        }

        public void BeginOneVanish(DiceOneVanishSettings settings, Action onComplete) {
            if (isVanishing || IsErasing || isCarried || board == null || diceView == null || settings == null) {
                return;
            }

            isVanishing = true;
            diceView.PlayOneVanish(settings, () => {
                registry?.Unregister(this);
                Erased?.Invoke(this);
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }

        public void OnBecameErasureGhost() {
            if (!IsErasureGhost) {
                return;
            }

            registry?.RemoveFromGrid(this);
            BecameErasureGhost?.Invoke(this);
        }

        public void OnBottomSupportLost(DiceController removedBottom) {
            if (currentState.Tier != DiceStackTier.Top) {
                return;
            }

            DemoteAfterSupportRemoved(removedBottom);
        }

        public void DemoteAfterSupportRemoved(DiceController removedBottom) {
            if (isCarried
                || IsErasing
                || isVanishing
                || board == null
                || diceView == null
                || registry == null
                || currentState.Tier != DiceStackTier.Top) {
                return;
            }

            if (removedBottom != null && removedBottom.IsErasureGhost) {
                removedBottom.CompleteErasureFromOverride();
            }

            var fromWorld = diceView.DiceTransform.position;
            var fromState = currentState;
            var toState = new DiceState(fromState.GridPos, fromState.Orientation, DiceStackTier.Bottom, fromState.Kind);
            ApplyLogicalMove(fromState, toState);
            isRolling = true;

            var transition = DiceTransition.CrushDemote(fromState, toState, fromWorld);
            diceView.PlayTransition(transition, board, registry, () => {
                isRolling = false;
                tierFallMatchNotifier?.NotifyTierFallCompleted(this);
                StateChanged?.Invoke(currentState);
            });
        }

        public void NotifyStackedTopSync() {
            registry?.SyncStackedTopAt(currentState.GridPos, board);
        }

        public void OnCeasedErasureGhost() {
            if (IsErasureGhost || !IsSinkErasing) {
                return;
            }

            registry?.RestoreToGrid(this);
        }

        public void CompleteErasureFromOverride() {
            if (!IsErasing) {
                return;
            }

            diceView?.CancelErasure();
            erasureKind = ErasureKind.None;
            registry?.Unregister(this);
            Erased?.Invoke(this);
            Destroy(gameObject);
        }

        public bool TryBeginCarry(Vector3 carryWorldTarget, Action onComplete) {
            if (IsBusy || isVanishing || board == null || diceView == null || diceView.DiceTransform == null) {
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

            var fromState = currentState;
            var toState = new DiceState(targetGrid, currentState.Orientation, targetTier, currentState.Kind);
            var toWorld = diceView.GetAnchoredWorldPosition(toState, board, registry);
            var transition = DiceTransition.FreeMove(fromWorld, toWorld, snapToGridOnComplete: true, toState);

            diceView.PlayTransition(transition, board, registry, () => {
                currentState = toState;
                isCarried = false;
                registry.Place(this, targetGrid, targetTier);
                ConfigurePushBody();
                NotifyActionMoveCompleted(fromState, toState);
                StateChanged?.Invoke(currentState);
                onComplete?.Invoke();
            });

            return true;
        }
    }
}
