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
        DiceMatchOwnershipContext ownershipContext;
        ITierFallMatchNotifier tierFallMatchNotifier;
        DiceState currentState;
        bool isRolling;
        bool isSpawning;
        ErasureKind erasureKind = ErasureKind.None;
        bool isVanishing;
        bool isCarried;
        bool isInitialized;
        DiceSpawnAppearMode spawnAppearMode = DiceSpawnAppearMode.None;
        Direction pendingElasticSlideDirection;
        DiceController pendingElasticTransferTarget;
        PlayerSlot pendingElasticActionOwner;
        bool hasPendingElasticTransfer;
        Action pendingSlideComplete;
        float logicalBusyRemaining;
        float logicalBusyDuration;
        Action pendingLogicalComplete;
        float logicalSpawnRemaining;
        Action pendingSpawnComplete;

        public bool IsSpawning => isSpawning;
        public DiceSpawnAppearMode SpawnAppearMode => spawnAppearMode;
        public bool AllowsUnconditionalMount =>
            spawnAppearMode == DiceSpawnAppearMode.BottomEmergence;
        /// <summary>
        /// Logical roll/slide busy only (not view animation). Advanced via <see cref="TickLogicalMotion"/>.
        /// </summary>
        public bool IsRolling => !isSpawning && isRolling;
        /// <summary>
        /// True while the dice is in a motion the standing player should follow
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
        /// <summary>
        /// True while this jumbo currently occupies Top footprint slots.
        /// Driven by <see cref="DiceRegistry.SyncJumboSinkOccupancy"/> (progress threshold + stack gate).
        /// </summary>
        public bool KeepsJumboTopOccupancy =>
            !Capabilities.HasExpandedFootprint
            || (registry != null
                && registry.TryGetTopAt(currentState.GridPos, out var top)
                && top == this);

        /// <summary>
        /// Sink stage still tall enough for Top connection (progress below threshold, or not sinking).
        /// </summary>
        public bool WantsJumboTopOccupancy =>
            !Capabilities.HasExpandedFootprint
            || !IsSinkErasing
            || (diceView != null
                && diceView.ErasureProgress < JumboFootprint.SinkTopOccupancyThreshold);
        public bool IsCarried => isCarried;
        public bool IsBusy => IsRolling || isSpawning || IsErasing || isVanishing || isCarried;
        public DiceState CurrentState => currentState;
        public DiceKind Kind => currentState.Kind;
        public IDiceBehavior Behavior => DiceBehaviorResolver.GetBehavior(Kind);
        public DiceCapabilities Capabilities => Behavior.Capabilities;
        public EffectiveDiceBehavior EffectiveBehavior =>
            DiceEffectiveBehaviorFactory.For(this, registry);
        public bool IsPlayerMovable => registry != null && EffectiveBehavior.IsPlayerMovable;
        public bool CanJumpCoupleWithPlayer =>
            registry != null && EffectiveBehavior.CanJumpCoupleWithPlayer;
        public bool CrushesPlayerOnCover => Capabilities.CrushesPlayerOnCover;
        public DiceView View => diceView;
        public float GroundRollProgress {
            get {
                if (isRolling && logicalBusyDuration > 0f) {
                    return 1f - Mathf.Clamp01(logicalBusyRemaining / logicalBusyDuration);
                }

                return diceView != null ? diceView.GroundRollProgress : 0f;
            }
        }

        public float LogicalMotionProgress {
            get {
                if (logicalBusyDuration <= 0f) {
                    return isRolling ? 0f : 1f;
                }

                return 1f - Mathf.Clamp01(logicalBusyRemaining / logicalBusyDuration);
            }
        }

        public Vector3 GetLogicalCenterWorld() {
            if (board == null) {
                return transform.position;
            }

            var gridWorld = board.GridToWorld(currentState.GridPos);
            return new Vector3(gridWorld.x, GetLogicalTopSurfaceWorldY(), gridWorld.z);
        }

        public Vector2 GetLogicalCenterXZ() {
            var center = GetLogicalCenterWorld();
            return new Vector2(center.x, center.z);
        }

        public Bounds GetLogicalPushBounds() {
            if (board == null) {
                return new Bounds(transform.position, Vector3.one);
            }

            var center = GetLogicalCenterWorld();
            var size = board.CellSize;
            var height = size;
            return new Bounds(
                new Vector3(center.x, center.y - height * 0.25f, center.z),
                new Vector3(size, height, size));
        }

        /// <summary>
        /// Advance logical roll/spawn timers. Called once per lockstep tick (or offline Update).
        /// </summary>
        public void TickLogicalMotion(float deltaTime) {
            if (deltaTime <= 0f) {
                return;
            }

            if (isSpawning && pendingSpawnComplete != null) {
                logicalSpawnRemaining -= deltaTime;
                if (logicalSpawnRemaining <= 0f) {
                    logicalSpawnRemaining = 0f;
                    var spawnComplete = pendingSpawnComplete;
                    pendingSpawnComplete = null;
                    spawnComplete?.Invoke();
                }
            }

            if (isRolling && logicalBusyRemaining > 0f) {
                logicalBusyRemaining -= deltaTime;
                if (logicalBusyRemaining <= 0f) {
                    logicalBusyRemaining = 0f;
                    var complete = pendingLogicalComplete;
                    pendingLogicalComplete = null;
                    complete?.Invoke();
                }
            }
        }

        void StartLogicalBusy(float duration, Action onComplete) {
            isRolling = true;
            logicalBusyDuration = Mathf.Max(0.0001f, duration);
            logicalBusyRemaining = logicalBusyDuration;
            pendingLogicalComplete = onComplete;
        }

        void ClearLogicalBusyWithoutComplete() {
            isRolling = false;
            logicalBusyRemaining = 0f;
            logicalBusyDuration = 0f;
            pendingLogicalComplete = null;
        }

        void FinishLogicalBusy() {
            isRolling = false;
            logicalBusyRemaining = 0f;
            logicalBusyDuration = 0f;
            pendingLogicalComplete = null;
            CommitVisualToCurrentLogicalState();
        }

        /// <summary>
        /// Logical busy ends in Update before move coroutines resume; snap so match/erasure
        /// never dissolve against a mid-move transform.
        /// </summary>
        void CommitVisualToCurrentLogicalState() {
            if (diceView == null || board == null) {
                return;
            }

            diceView.SnapTo(currentState, board, registry);
        }

        const float LogicalMotionFallbackSeconds = 0.3f;

        /// <summary>
        /// Must resolve the same visual <see cref="PlayVisualForPlan"/> plays, so the logical
        /// state advances exactly when the motion ends.
        /// </summary>
        float ResolvePlanLogicalDuration(DiceGridMovePlan plan, DiceMoveVisualContext context) {
            if (diceView == null) {
                return LogicalMotionFallbackSeconds;
            }

            switch (plan.Kind) {
                case DiceGridMoveKind.Parallel:
                    return diceView.GetJumpRollLogicalDuration(
                        plan.From,
                        plan.To,
                        plan.Distance,
                        fallBeforeSnap: false,
                        useArcRoll: context.IsJump && context.JumpMotionProvider != null,
                        board,
                        registry,
                        context.JumpMotionProvider);
                case DiceGridMoveKind.Demote:
                    if (UsesSlideVisualForDemote(plan)) {
                        return ResolveSlideLogicalDuration(plan.From, plan.To);
                    }

                    goto case DiceGridMoveKind.Stack;
                case DiceGridMoveKind.Stack:
                    return context.IsJump
                        ? diceView.GetJumpRollLogicalDuration(
                            plan.From,
                            plan.To,
                            plan.Distance,
                            fallBeforeSnap: context.JumpMotionProvider == null,
                            useArcRoll: context.JumpMotionProvider != null,
                            board,
                            registry,
                            context.JumpMotionProvider)
                        : diceView.GetTransitionLogicalDuration(
                            BuildTierChangeTransition(plan),
                            board,
                            registry);
            }

            return LogicalMotionFallbackSeconds;
        }

        float ResolveSlideLogicalDuration(DiceState fromState, DiceState toState) {
            if (diceView == null) {
                return LogicalMotionFallbackSeconds;
            }

            return diceView.GetTransitionLogicalDuration(
                DiceTransition.GridMove(fromState, toState),
                board,
                registry,
                ResolveSlideCellDistance(fromState, toState));
        }

        static int ResolveSlideCellDistance(DiceState fromState, DiceState toState) {
            return Mathf.Max(
                1,
                MovementTransitionEvaluator.GetOrthogonalDistance(fromState.GridPos, toState.GridPos));
        }

        static bool UsesSlideVisualForDemote(DiceGridMovePlan plan) {
            return DiceBehaviorResolver
                .GetBehavior(plan.From.Kind)
                .Capabilities
                .UsesSlideVisualForDemote;
        }

        static DiceTransition BuildTierChangeTransition(DiceGridMovePlan plan) {
            return plan.Kind == DiceGridMoveKind.Stack
                ? DiceTransition.RollThenRise(plan.From, plan.To, plan.Direction)
                : DiceTransition.RollThenDemote(plan.From, plan.To, plan.Direction);
        }

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

        public void ConfigureOwnershipContext(DiceMatchOwnershipContext targetOwnershipContext) {
            ownershipContext = targetOwnershipContext;
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
            spawnAppearMode = DiceSpawnAppearMode.None;
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
            spawnAppearMode = !forceFallFromAbove && tier == DiceStackTier.Bottom
                ? DiceSpawnAppearMode.BottomEmergence
                : DiceSpawnAppearMode.FallFromAbove;
            currentState = new DiceState(gridPos, orientation, tier, kind);
            registry?.RegisterPendingSpawn(this, gridPos, tier);

            void OnSpawnComplete() {
                if (!isSpawning) {
                    return;
                }

                registry?.CommitPendingSpawn(this, currentState.GridPos, currentState.Tier);
                isSpawning = false;
                spawnAppearMode = DiceSpawnAppearMode.None;
                logicalSpawnRemaining = 0f;
                pendingSpawnComplete = null;
                ConfigurePushBody();
                StateChanged?.Invoke(currentState);
                onComplete?.Invoke();
            }

            pendingSpawnComplete = OnSpawnComplete;
            logicalSpawnRemaining = ResolveSpawnAppearLogicalDuration();

            if (spawnAppearMode == DiceSpawnAppearMode.BottomEmergence) {
                diceView.PlayBottomEmergenceAppear(
                    currentState,
                    board,
                    registry,
                    Capabilities.FallGravityScale,
                    null);
            } else {
                diceView.PlaySpawnAppear(
                    currentState,
                    board,
                    registry,
                    Capabilities.HasSpawnBounce,
                    Capabilities.FallGravityScale,
                    null);
            }
        }

        float ResolveSpawnAppearLogicalDuration() {
            if (diceView == null) {
                return LogicalMotionFallbackSeconds;
            }

            return spawnAppearMode == DiceSpawnAppearMode.BottomEmergence
                ? diceView.GetBottomEmergenceLogicalDuration()
                : diceView.GetSpawnFallLogicalDuration(
                    Capabilities.HasSpawnBounce,
                    Capabilities.FallGravityScale);
        }

        /// <summary>
        /// Called by <see cref="DiceRegistry"/> when a committed Bottom claims this die's
        /// pending Bottom cell — reservation is already moved to Top.
        /// </summary>
        public void NotifyPendingSpawnRetargetedToTop() {
            if (!isSpawning || currentState.Tier == DiceStackTier.Top) {
                return;
            }

            var wasEmerging = spawnAppearMode == DiceSpawnAppearMode.BottomEmergence;
            currentState = new DiceState(
                currentState.GridPos,
                currentState.Orientation,
                DiceStackTier.Top,
                currentState.Kind);
            spawnAppearMode = DiceSpawnAppearMode.FallFromAbove;
            diceView?.RetargetActiveSpawnLanding(currentState);

            // Emergence → Top: settled Bottom rest is below Top rest, so drop height is 0 and
            // logical spawn completes on the next tick. Never sample View motion Offset — that
            // follows stall/frame presentation and is not lockstep-deterministic.
            if (wasEmerging) {
                logicalSpawnRemaining = 0f;
            }

            StateChanged?.Invoke(currentState);
        }

        void ConfigurePushBody() {
            var pushBody = GetComponentInChildren<DicePushBody>();
            pushBody?.Configure(board);
            // Pass-through Ghost disables push; sink-erasing Ghost is solid again.
            pushBody?.SetCollisionEnabled(!GhostPlacementRules.IsPlayerPassThrough(this));
        }

        public void ApplyExternalState(DiceState state, bool snapVisual = false) {
            currentState = state;
            if (snapVisual && diceView != null && board != null && registry != null) {
                diceView.SnapTo(currentState, board, registry);
            }

            StateChanged?.Invoke(currentState);
        }

        public void PlayGhostDisplaceVisual(DiceState fromState, DiceState toState, Action onComplete = null) {
            if (diceView == null || board == null || registry == null) {
                onComplete?.Invoke();
                return;
            }

            // Ghost displace is always an instant warp (no slide interpolation).
            diceView.SnapTo(toState, board, registry);
            onComplete?.Invoke();
        }

        public float GetTopSurfaceWorldY() {
            return diceView != null && board != null
                ? diceView.GetTopSurfaceWorldY(board)
                : board != null ? board.FloorSurfaceWorldY : 0f;
        }

        public float GetLogicalTopSurfaceWorldY() {
            if (Capabilities.HasExpandedFootprint) {
                return GetLogicalStandingSurfaceWorldY(
                    KeepsJumboTopOccupancy ? SurfaceHeightLevel.Top : SurfaceHeightLevel.Bottom);
            }

            if (diceView == null || board == null) {
                return board != null ? board.FloorSurfaceWorldY : 0f;
            }

            // Bottom tier is always floor-anchored so stacked ghosts/tops never read animated fall Y.
            if (currentState.Tier == DiceStackTier.Bottom) {
                return diceView.GetLogicalBottomTierTopSurfaceWorldY(board);
            }

            return diceView.GetLogicalTopSurfaceWorldY(board);
        }

        /// <summary>
        /// Standing / stack surface Y for a height level. Jumbo is 2× tall: Top = 2 cells, Bottom = 1.
        /// While sink-erasing, height follows visual squash (same as normal dice) so floor mounts
        /// become reachable once the step limit is met.
        /// </summary>
        public float GetLogicalStandingSurfaceWorldY(int surfaceLevel) {
            if (board == null) {
                return 0f;
            }

            if (Capabilities.HasExpandedFootprint) {
                if (IsSinkErasing && diceView != null) {
                    return diceView.GetLogicalBottomTierTopSurfaceWorldY(board);
                }

                var tiers = surfaceLevel == SurfaceHeightLevel.Top && KeepsJumboTopOccupancy
                    ? JumboFootprint.Size
                    : 1;
                return board.FloorSurfaceWorldY + board.CellSize * tiers;
            }

            return GetLogicalTopSurfaceWorldY();
        }

        public bool TryExecuteSlidePlan(DiceSlidePlan plan, PlayerSlot actionOwner) {
            return TryExecuteSlidePlan(
                plan,
                actionOwner,
                slideDirection: null,
                elasticTransferTarget: null,
                onSlideComplete: null);
        }

        public bool TryExecuteSlidePlan(
            DiceSlidePlan plan,
            PlayerSlot actionOwner,
            Direction slideDirection,
            DiceController elasticTransferTarget,
            Action onSlideComplete = null) {
            return TryExecuteSlidePlan(
                plan,
                actionOwner,
                (Direction?)slideDirection,
                elasticTransferTarget,
                onSlideComplete);
        }

        public bool TryExecuteSlidePlan(
            DiceSlidePlan plan,
            PlayerSlot actionOwner,
            Direction? slideDirection,
            DiceController elasticTransferTarget,
            Action onSlideComplete = null) {
            if (IsBusy || IsErasing || isVanishing || board == null || diceView == null || registry == null) {
                return false;
            }

            if (Capabilities.HasMagnetCoupling) {
                return MagnetMoveExecutor.TryExecuteSlide(this, plan, registry, matchActionContext, actionOwner);
            }

            return TryExecuteSlidePlanInternal(
                plan,
                actionOwner,
                slideDirection,
                elasticTransferTarget,
                onSlideComplete);
        }

        internal bool TryExecuteSlidePlanInternal(DiceSlidePlan plan) {
            return TryExecuteSlidePlanInternal(plan, PlayerSlot.Player1, null, null, null);
        }

        internal bool TryExecuteSlidePlanInternal(
            DiceSlidePlan plan,
            PlayerSlot actionOwner,
            Direction? slideDirection,
            DiceController elasticTransferTarget,
            Action onSlideComplete = null) {
            if (IsBusy || IsErasing || isVanishing || board == null || diceView == null || registry == null) {
                return false;
            }

            return BeginSlide(plan, actionOwner, slideDirection, elasticTransferTarget, onSlideComplete);
        }

        public bool TryExecuteGroundMovePlan(DiceGridMovePlan plan, PassabilityContext context) {
            if (IsErasing || isVanishing || isCarried || isSpawning || isRolling || board == null || diceView == null || registry == null) {
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
            ClearLogicalBusyWithoutComplete();
            ClearPendingElasticTransfer();
            pendingSlideComplete = null;
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
            if (IsErasing || isVanishing || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (!snapshot.IsValid) {
                return false;
            }

            ApplyLogicalMove(plan.From, plan.To);
            var duration = diceView != null
                ? diceView.GetCancelRollLogicalDuration(cancelProgress)
                : LogicalMotionFallbackSeconds;
            StartLogicalBusy(duration, () => {
                FinishLogicalBusy();
                NotifyActionMoveCompleted();
                StateChanged?.Invoke(currentState);
            });
            diceView.PlayCancelGroundRollVisual(
                snapshot,
                plan.To,
                cancelProgress,
                board,
                registry,
                null);

            return true;
        }

        public bool TryExecuteCancelJumpMovePlan(
            DiceGridMovePlan plan,
            DiceRollVisualSnapshot snapshot,
            Func<VerticalMotionState> jumpMotionProvider) {
            if (IsErasing || isVanishing || isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (!snapshot.IsValid || jumpMotionProvider == null) {
                return false;
            }

            ApplyLogicalMove(plan.From, plan.To);
            var duration = diceView != null
                ? diceView.GetJumpRollLogicalDuration(
                    plan.From,
                    plan.To,
                    plan.Distance,
                    fallBeforeSnap: false,
                    useArcRoll: true,
                    board,
                    registry,
                    jumpMotionProvider)
                : LogicalMotionFallbackSeconds;
            StartLogicalBusy(duration, () => {
                FinishLogicalBusy();
                NotifyActionMoveCompleted(plan.From, plan.To);
                StateChanged?.Invoke(currentState);
            });
            diceView.PlayCancelJumpParallelRollVisual(
                snapshot,
                plan,
                board,
                registry,
                null,
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

        bool ApplyLogicalMoveWithGhost(
            DiceState fromState,
            DiceState toState,
            GhostLandingMode ghostLanding,
            DiceState ghostFrom,
            DiceState ghostTo) {
            if (ghostLanding == GhostLandingMode.None) {
                ApplyLogicalMove(fromState, toState);
                return true;
            }

            currentState = toState;
            if (!registry.TryApplyGhostLanding(this, fromState, toState, ghostLanding, ghostFrom, ghostTo)) {
                currentState = fromState;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Move the mover now; ghost slot is vacated but displace waits for animation complete.
        /// </summary>
        bool BeginLogicalMoveDeferringGhost(
            DiceState fromState,
            DiceState toState,
            GhostLandingMode ghostLanding,
            DiceState ghostFrom) {
            if (ghostLanding == GhostLandingMode.None) {
                ApplyLogicalMove(fromState, toState);
                return true;
            }

            if (!registry.TryDeferGhostOccupant(ghostFrom, out _)) {
                return false;
            }

            ApplyLogicalMove(fromState, toState);
            return true;
        }

        void CompleteDeferredGhostLanding(
            GhostLandingMode ghostLanding,
            DiceState ghostFrom,
            DiceState ghostTo,
            Action onComplete = null) {
            if (ghostLanding == GhostLandingMode.None || registry == null) {
                onComplete?.Invoke();
                return;
            }

            registry.TryCompleteDeferredGhostLanding(ghostFrom, ghostTo, ghost => {
                RegisterDisplacedGhostForMatch(ghost);
                // If a same-tier Top swap left the ghost unsupported, demote it now.
                if (ghost != null) {
                    registry.ResolveUnsupportedTopAt(ghost.CurrentState.GridPos);
                }

                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Ghost displace can form matches without the mover; register it as an action participant.
        /// </summary>
        void RegisterDisplacedGhostForMatch(DiceController ghost) {
            if (ghost == null || matchActionContext == null) {
                return;
            }

            if (!TryResolveDisplaceActionOwner(out var owner)) {
                return;
            }

            matchActionContext.RegisterActionDice(ghost, owner);
        }

        bool TryResolveDisplaceActionOwner(out PlayerSlot owner) {
            if (matchActionContext != null
                && matchActionContext.TryGetActionOwner(this, out owner)) {
                return true;
            }

            if (ownershipContext != null
                && ownershipContext.TryGetTierFallSupportOwner(this, out owner)) {
                return true;
            }

            if (ownershipContext != null
                && ownershipContext.TryGetOwner(this, out owner)) {
                return true;
            }

            owner = default;
            return false;
        }

        void NotifyActionMoveCompleted(DiceState fromState, DiceState toState) {
            if (PlayerMatchActionContext.IsActionParticipationMove(fromState, toState)) {
                matchActionContext?.NotifyParticipantMoveCompleted(this);
            }
        }

        void NotifyActionMoveCompleted() {
            matchActionContext?.NotifyParticipantMoveCompleted(this);
        }

        bool TryExecuteMovePlan(DiceGridMovePlan plan, DiceMoveVisualContext context) {
            if (IsErasing || isVanishing || isCarried || isSpawning || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            if (!BeginLogicalMoveDeferringGhost(
                plan.From,
                plan.To,
                plan.GhostLanding,
                plan.GhostFrom)) {
                return false;
            }

            StartLogicalBusy(
                ResolvePlanLogicalDuration(plan, context),
                () => {
                    CompleteDeferredGhostLanding(
                        plan.GhostLanding,
                        plan.GhostFrom,
                        plan.GhostTo,
                        () => {
                            FinishLogicalBusy();
                            registry.ResolveUnsupportedTopAt(currentState.GridPos);
                            NotifyActionMoveCompleted(plan.From, plan.To);
                            StateChanged?.Invoke(currentState);
                        });
                });
            PlayVisualForPlan(plan, context, null);

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
                    if (UsesSlideVisualForDemote(plan)) {
                        PlaySlideVisual(plan.From, plan.To, onComplete);
                        return;
                    }

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
            diceView.PlayTransition(BuildTierChangeTransition(plan), board, registry, onComplete);
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
            diceView.PlayTransition(
                DiceTransition.GridMove(fromState, toState),
                board,
                registry,
                onComplete,
                ResolveSlideCellDistance(fromState, toState));
        }

        bool BeginSlide(
            DiceSlidePlan plan,
            PlayerSlot actionOwner,
            Direction? slideDirection,
            DiceController elasticTransferTarget,
            Action onSlideComplete) {
            if (!BeginLogicalMoveDeferringGhost(
                plan.From,
                plan.To,
                plan.GhostLanding,
                plan.GhostFrom)) {
                return false;
            }

            var resolvedDirection = slideDirection;
            if (!resolvedDirection.HasValue
                && MovementTransitionEvaluator.TryGetDirectionBetween(
                    plan.From.GridPos,
                    plan.To.GridPos,
                    out var inferredDirection)) {
                resolvedDirection = inferredDirection;
            }

            hasPendingElasticTransfer = Capabilities.TransfersSlideOnCollision
                && resolvedDirection.HasValue
                && elasticTransferTarget != null;
            if (hasPendingElasticTransfer) {
                pendingElasticSlideDirection = resolvedDirection.Value;
                pendingElasticTransferTarget = elasticTransferTarget;
                pendingElasticActionOwner = actionOwner;
            } else {
                ClearPendingElasticTransfer();
            }

            pendingSlideComplete = onSlideComplete;

            StartLogicalBusy(
                ResolveSlideLogicalDuration(plan.From, plan.To),
                () => {
                    CompleteDeferredGhostLanding(
                        plan.GhostLanding,
                        plan.GhostFrom,
                        plan.GhostTo,
                        () => {
                            FinishLogicalBusy();
                            registry.ResolveUnsupportedTopAt(currentState.GridPos);
                            NotifyActionMoveCompleted(plan.From, plan.To);
                            StateChanged?.Invoke(currentState);
                            var slideComplete = pendingSlideComplete;
                            pendingSlideComplete = null;
                            CompletePendingElasticTransfer();
                            slideComplete?.Invoke();
                        });
                });
            PlaySlideVisual(plan.From, plan.To, null);

            return true;
        }

        void ClearPendingElasticTransfer() {
            hasPendingElasticTransfer = false;
            pendingElasticTransferTarget = null;
        }

        void CompletePendingElasticTransfer() {
            if (!hasPendingElasticTransfer) {
                return;
            }

            var direction = pendingElasticSlideDirection;
            var target = pendingElasticTransferTarget;
            var owner = pendingElasticActionOwner;
            ClearPendingElasticTransfer();
            IceElasticSlideExecutor.TryBeginTransfer(target, direction, registry, owner);
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
            // Sink starts: Ghost becomes solid for the player again.
            if (kind == ErasureKind.Sink && Capabilities.IsPlayerPassThrough) {
                ConfigurePushBody();
            }

            diceView.PlayErasure(kind, currentState, board, registry, emissionColor, () => {
                registry?.Unregister(this);
                erasureKind = ErasureKind.None;
                Erased?.Invoke(this);
                onComplete?.Invoke();
                Destroy(gameObject);
            });
        }

        public void BeginErasureForCurrentTier(Color? emissionColor, Action onComplete) {
            var kind = Capabilities.HasExpandedFootprint || currentState.Tier != DiceStackTier.Top
                ? ErasureKind.Sink
                : ErasureKind.Radiance;
            BeginErasure(kind, emissionColor, onComplete);
        }

        public void SetErasureEmissionColor(Color emissionColor) {
            diceView?.SetErasureEmissionColor(emissionColor);
        }

        public void RetreatErasure(float amount) {
            if (!IsErasing || diceView == null) {
                return;
            }

            // Match sink progress rate: Jumbo (duration ×2) retreats half as far in progress space.
            var sinkMultiplier = Capabilities.SinkDurationMultiplier;
            var scaledAmount = sinkMultiplier > 0f ? amount / sinkMultiplier : amount;
            diceView.RetreatErasure(scaledAmount);
        }

        public void AdvanceErasure(float amount) {
            if (!IsSinkErasing || diceView == null) {
                return;
            }

            if (Capabilities.BlocksJumpLandingSinkAdvance) {
                return;
            }

            diceView.AdvanceErasure(amount);
        }

        public void BeginOneVanish(DiceOneVanishSettings settings, Color emissionColor, Action onComplete) {
            if (isVanishing || IsErasing || isCarried || board == null || diceView == null || settings == null) {
                return;
            }

            isVanishing = true;
            diceView.SetErasureEmissionColor(emissionColor);
            diceView.PlayOneVanish(settings, currentState, board, registry, () => {
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

        /// <summary>
        /// Generic unsupported fall: Top with no Bottom demotes into Bottom (never floats).
        /// Fall speed uses <see cref="DiceCapabilities.FallGravityScale"/>.
        /// </summary>
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

            ownershipContext?.CaptureTierFallSupportOwner(this, removedBottom);

            if (removedBottom != null && removedBottom.IsErasureGhost) {
                removedBottom.CompleteErasureFromOverride();
            }

            var fromWorld = diceView.DiceTransform.position;
            var fromState = currentState;
            var toState = new DiceState(fromState.GridPos, fromState.Orientation, DiceStackTier.Bottom, fromState.Kind);

            var deferredGhostLanding = GhostLandingMode.None;
            var deferredGhostFrom = default(DiceState);
            var deferredGhostTo = default(DiceState);

            // Vacate ghost Bottom for the fall; promote after fall animation completes.
            if (registry.TryGetBottomAt(fromState.GridPos, out var ghostBottom)
                && GhostPlacementRules.TryResolveInCellPromote(
                    fromState,
                    ghostBottom,
                    out _,
                    out deferredGhostFrom,
                    out deferredGhostTo)) {
                if (!registry.TryDeferGhostOccupant(deferredGhostFrom, out _)) {
                    return;
                }

                deferredGhostLanding = GhostLandingMode.InCellPromoteGhost;
            }

            var transition = DiceTransition.CrushDemote(fromState, toState, fromWorld);

            ApplyLogicalMove(fromState, toState);
            StartLogicalBusy(
                diceView.GetTransitionLogicalDuration(transition, board, registry),
                () => {
                    CompleteDeferredGhostLanding(
                        deferredGhostLanding,
                        deferredGhostFrom,
                        deferredGhostTo,
                        () => {
                            FinishLogicalBusy();
                            tierFallMatchNotifier?.NotifyTierFallCompleted(this);
                            matchActionContext?.NotifyParticipantMoveCompleted(this);
                            StateChanged?.Invoke(currentState);
                        });
                });

            diceView.PlayTransition(transition, board, registry, null);
        }

        public void NotifyStackedTopSync() {
            registry?.SyncStackedTopsForDice(this, board);
        }

        /// <summary>
        /// Called whenever sink erasure progress changes so Jumbo Top/Bottom occupancy stays in sync.
        /// </summary>
        public void NotifyErasureProgressChanged() {
            if (!Capabilities.HasExpandedFootprint || !IsSinkErasing || registry == null) {
                return;
            }

            var topChanged = registry.SyncJumboSinkOccupancy(this);
            registry.SyncStackedTopsForDice(this, board);
            if (topChanged) {
                StateChanged?.Invoke(currentState);
            }
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

        /// <summary>
        /// Immediately remove this die (including mid-erasure / vanish) for jumbo landing clears.
        /// </summary>
        public void ForceDestroyForOverride() {
            if (IsErasing) {
                CompleteErasureFromOverride();
                return;
            }

            isVanishing = false;
            isSpawning = false;
            logicalSpawnRemaining = 0f;
            pendingSpawnComplete = null;
            ClearLogicalBusyWithoutComplete();
            diceView?.CancelErasure();
            registry?.Unregister(this);
            Erased?.Invoke(this);
            Destroy(gameObject);
        }

        public bool TryBeginCarry(Vector3 carryWorldTarget, Action onComplete) {
            if (IsBusy || isVanishing || board == null || diceView == null || diceView.DiceTransform == null) {
                return false;
            }

            isCarried = true;
            // Vacate occupancy only — stay in allDice so TickLogicalMotions advances lift/place busy.
            registry?.RemoveFromGrid(this);

            // Logical from-position (not mid-lerp view transform) so both lockstep peers match.
            var fromWorld = diceView.GetAnchoredWorldPosition(currentState, board, registry);
            var transition = DiceTransition.FreeMove(fromWorld, carryWorldTarget, snapToGridOnComplete: false);
            StartLogicalBusy(
                diceView.GetTransitionLogicalDuration(transition, board, registry),
                () => {
                    // Still carried: do not snap back onto the vacated cell.
                    ClearLogicalBusyWithoutComplete();
                    onComplete?.Invoke();
                });
            diceView.PlayTransition(transition, board, registry, null);

            return true;
        }

        public bool TryPlaceAt(Vector2Int targetGrid, DiceStackTier targetTier, Vector3 fromWorld, Action onComplete) {
            if (!isCarried || isRolling || board == null || diceView == null || registry == null) {
                return false;
            }

            var toState = new DiceState(targetGrid, currentState.Orientation, targetTier, currentState.Kind);
            var toWorld = diceView.GetAnchoredWorldPosition(toState, board, registry);
            var transition = DiceTransition.FreeMove(fromWorld, toWorld, snapToGridOnComplete: true, toState);

            StartLogicalBusy(
                diceView.GetTransitionLogicalDuration(transition, board, registry),
                () => {
                    currentState = toState;
                    isCarried = false;
                    registry.Place(this, targetGrid, targetTier);
                    ConfigurePushBody();
                    FinishLogicalBusy();
                    // Carry placement always completes the player action, including same-cell drops.
                    NotifyActionMoveCompleted();
                    StateChanged?.Invoke(currentState);
                    onComplete?.Invoke();
                });
            diceView.PlayTransition(transition, board, registry, null);

            return true;
        }
    }
}
