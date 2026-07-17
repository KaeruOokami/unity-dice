using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DiceGame.View
{
    public class DiceView : MonoBehaviour
    {
        [SerializeField] UnityEngine.Object diceMeshPrefab;
        [SerializeField] Transform positionRoot;
        [SerializeField] Transform rotationRoot;
        [SerializeField] Transform dissolvePivot;

        PhysicsSettings physicsSettings;
        DiceAnimationSettings animationSettings;
        DiceErasureSettings erasureSettings;

        Transform meshInstance;
        Coroutine rollCoroutine;
        Coroutine erasureCoroutine;
        Coroutine oneVanishCoroutine;
        bool isAnimating;
        float erasureProgress;
        ErasureKind activeErasureKind = ErasureKind.None;
        float groundRollProgress;
        int currentTopFace = 1;
        Vector3 gridWorldPosition;
        float surfaceBaseWorldY;
        float visualYOffset;
        Board erasureBoard;
        DicePushBody pushBody;
        DiceController diceController;
        bool wasErasureGhost;
        readonly List<Material> dissolveMaterials = new();
        readonly List<Color> dissolveMaterialBaseColors = new();
        readonly List<Color> dissolveMaterialBaseEmissionColors = new();
        readonly List<Texture> dissolveMaterialBaseEmissionMaps = new();
        readonly List<bool> dissolveMaterialHadEmission = new();
        bool dissolveMaterialsTransparent;
        GameObject runtimeMeshPrefab;
        Texture dissolveEmissionMapOverride;
        Color? erasureEmissionColorOverride;

        // Dice mesh is visual-only. Gameplay uses `Board.CellSize` as the "logical dice cube" size.
        // So we measure the mesh's local bounds once, then scale/center it to match `Board.CellSize`.
        float cachedMeshUnitMaxExtent = -1f;
        Vector3 cachedMeshLocalBoundsCenter;
        float appliedCellSize = float.NaN;

        public bool IsAnimating => isAnimating;
        public float ErasureProgress => erasureProgress;
        public float GroundRollProgress => groundRollProgress;
        public bool IsErasureGhost =>
            activeErasureKind == ErasureKind.Sink
            && erasureSettings != null
            && erasureProgress >= erasureSettings.SinkGhostThreshold;

        public Transform DiceTransform => positionRoot;

        public void Configure(
            PhysicsSettings physics,
            DiceAnimationSettings animation,
            DiceErasureSettings erasure) {
            physicsSettings = physics;
            animationSettings = animation;
            erasureSettings = erasure;
        }

        public void SetMeshPrefab(GameObject prefab) {
            if (prefab == null) {
                Debug.LogError("DiceView: SetMeshPrefab received null prefab.");
                return;
            }

            runtimeMeshPrefab = prefab;
            if (meshInstance != null) {
                Destroy(meshInstance.gameObject);
                meshInstance = null;
            }

            cachedMeshUnitMaxExtent = -1f;
            cachedMeshLocalBoundsCenter = Vector3.zero;
            appliedCellSize = float.NaN;

            EnsureMesh();
        }

        void Awake() {
            ResolveHierarchy();
        }

        void ResolveHierarchy() {
            if (positionRoot == null) {
                positionRoot = transform.Find("PositionRoot");
            }

            if (rotationRoot == null && positionRoot != null) {
                rotationRoot = positionRoot.Find("RotationRoot");
            }

            if (dissolvePivot == null && rotationRoot != null) {
                dissolvePivot = rotationRoot.Find("DissolvePivot");
            }
        }

        public void EnsureDiceInstance() {
            EnsureMesh();
        }

        void EnsureMesh() {
            if (meshInstance != null) {
                return;
            }

            ResolveHierarchy();
            if (dissolvePivot == null) {
                Debug.LogError("DiceView: DissolvePivot is not assigned. Run Dice > Create DiceEntity Prefab.");
                return;
            }

            var prefab = ResolveMeshPrefab();
            if (prefab == null) {
                return;
            }

            var visual = UnityEngine.Object.Instantiate(prefab, dissolvePivot);
            visual.name = "DiceMesh";
            meshInstance = visual.transform;
            meshInstance.localPosition = Vector3.zero;
            meshInstance.localRotation = Quaternion.identity;
            // Normalize first so our cached "unit" bounds are stable.
            meshInstance.localScale = Vector3.one;
            dissolveEmissionMapOverride = ResolveBaseMapFromPrefab(prefab);
            CacheDissolveMaterials();
            CacheMeshLocalBounds();
        }

        void CacheMeshLocalBounds() {
            cachedMeshUnitMaxExtent = -1f;
            cachedMeshLocalBoundsCenter = Vector3.zero;

            if (meshInstance == null) {
                return;
            }

            var renderers = meshInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) {
                Debug.LogError("DiceView: Dice mesh has no Renderer components to measure.");
                return;
            }

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            // Build a local-space AABB aligned to `meshInstance` axes from all renderer local bounds.
            foreach (var renderer in renderers) {
                var localBounds = renderer.localBounds;
                var r = renderer.transform;

                var corners = new Vector3[8] {
                    new(localBounds.min.x, localBounds.min.y, localBounds.min.z),
                    new(localBounds.min.x, localBounds.min.y, localBounds.max.z),
                    new(localBounds.min.x, localBounds.max.y, localBounds.min.z),
                    new(localBounds.min.x, localBounds.max.y, localBounds.max.z),
                    new(localBounds.max.x, localBounds.min.y, localBounds.min.z),
                    new(localBounds.max.x, localBounds.min.y, localBounds.max.z),
                    new(localBounds.max.x, localBounds.max.y, localBounds.min.z),
                    new(localBounds.max.x, localBounds.max.y, localBounds.max.z),
                };

                for (var i = 0; i < corners.Length; i++) {
                    var world = r.TransformPoint(corners[i]);
                    var local = meshInstance.InverseTransformPoint(world);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }

            var size = max - min;
            cachedMeshUnitMaxExtent = Mathf.Max(size.x, size.y, size.z);
            cachedMeshLocalBoundsCenter = (min + max) * 0.5f;
        }

        void ApplyMeshVisualScale(Board board) {
            if (meshInstance == null || board == null) {
                return;
            }

            if (!float.IsNaN(appliedCellSize) && Mathf.Abs(appliedCellSize - board.CellSize) < 0.0001f) {
                return;
            }

            if (cachedMeshUnitMaxExtent <= Mathf.Epsilon) {
                CacheMeshLocalBounds();
            }

            if (cachedMeshUnitMaxExtent <= Mathf.Epsilon) {
                // Can't scale reliably. Leave the mesh as-is.
                return;
            }

            var scale = board.CellSize / cachedMeshUnitMaxExtent;
            meshInstance.localScale = Vector3.one * scale;
            // Center mesh geometry on the rotationRoot origin.
            meshInstance.localPosition = -cachedMeshLocalBoundsCenter * scale;
            appliedCellSize = board.CellSize;
        }

        void EnsurePushBody() {
            if (pushBody == null) {
                pushBody = GetComponentInChildren<DicePushBody>(true);
            }
        }

        void EnsureDiceController() {
            if (diceController == null) {
                diceController = GetComponent<DiceController>();
            }
        }

        void CacheDissolveMaterials() {
            dissolveMaterials.Clear();
            dissolveMaterialBaseColors.Clear();
            dissolveMaterialBaseEmissionColors.Clear();
            dissolveMaterialBaseEmissionMaps.Clear();
            dissolveMaterialHadEmission.Clear();
            dissolveMaterialsTransparent = false;

            if (meshInstance == null) {
                return;
            }

            foreach (var renderer in meshInstance.GetComponentsInChildren<Renderer>(true)) {
                var sourceMaterials = renderer.sharedMaterials;
                var instances = new Material[sourceMaterials.Length];
                for (var i = 0; i < sourceMaterials.Length; i++) {
                    if (sourceMaterials[i] == null) {
                        continue;
                    }

                    instances[i] = new Material(sourceMaterials[i]);
                    dissolveMaterials.Add(instances[i]);
                    dissolveMaterialBaseColors.Add(GetMaterialBaseColor(instances[i]));
                    dissolveMaterialBaseEmissionColors.Add(GetMaterialEmissionColor(instances[i]));
                    dissolveMaterialBaseEmissionMaps.Add(GetMaterialEmissionMap(instances[i]));
                    dissolveMaterialHadEmission.Add(instances[i].IsKeywordEnabled("_EMISSION"));
                }

                renderer.materials = instances;
            }
        }

        GameObject ResolveMeshPrefab() {
            if (runtimeMeshPrefab != null) {
                return runtimeMeshPrefab;
            }

            switch (diceMeshPrefab) {
                case GameObject prefab:
                    return prefab;
                case null:
                    Debug.LogError("DiceView: diceMeshPrefab is not assigned. Assign Dice_d6 prefab on DiceEntity.");
                    return null;
                default:
                    Debug.LogError(
                        $"DiceView: diceMeshPrefab must be a GameObject prefab asset. Got {diceMeshPrefab.GetType().Name}. Run Dice > Create DiceEntity Prefab.");
                    return null;
            }
        }

        public float GetTopSurfaceWorldY(Board board) {
            if (board == null || positionRoot == null || rotationRoot == null) {
                return 0f;
            }

            ComputeVerticalExtents(board, currentTopFace, 1f - erasureProgress, out _, out var maxY);
            return positionRoot.position.y + maxY;
        }

        public float GetLogicalTopSurfaceWorldY(Board board) {
            if (board == null || rotationRoot == null) {
                return 0f;
            }

            var squash = 1f - erasureProgress;
            ComputeVerticalExtents(board, currentTopFace, squash, out var minY, out var maxY);
            return surfaceBaseWorldY - minY + maxY;
        }

        public void SnapTo(DiceState state, Board board, DiceRegistry registry = null) {
            InterruptRollAnimation();

            if (erasureCoroutine != null) {
                StopCoroutine(erasureCoroutine);
                erasureCoroutine = null;
            }

            isAnimating = false;
            erasureProgress = 0f;
            erasureBoard = null;
            wasErasureGhost = false;
            activeErasureKind = ErasureKind.None;
            visualYOffset = 0f;
            currentTopFace = state.Orientation.Top;
            EnsureMesh();
            if (dissolvePivot == null || rotationRoot == null || positionRoot == null) {
                return;
            }

            positionRoot.SetParent(transform);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            gridWorldPosition = board.GridToWorld(state.GridPos);
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(state.Orientation);
            UpdateSurfaceBase(state, board, registry);
            ResetErasureVisuals();
            ApplyErasureVisual(board, 0f);
        }

        public Vector3 GetAnchoredWorldPosition(DiceState state, Board board, DiceRegistry registry) {
            EnsureMesh();
            ApplyMeshVisualScale(board);
            return ComputeAnchoredWorldPosition(state, board, registry, visualYOffset);
        }

        Vector3 ComputeAnchoredWorldPosition(
            DiceState state,
            Board board,
            DiceRegistry registry,
            float yOffset) {
            if (positionRoot == null || rotationRoot == null || board == null) {
                return Vector3.zero;
            }

            var grid = board.GridToWorld(state.GridPos);
            var savedRotation = rotationRoot.rotation;
            var savedFace = currentTopFace;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(state.Orientation);
            currentTopFace = state.Orientation.Top;
            ComputeVerticalExtents(board, state.Orientation.Top, 1f - erasureProgress, out var minY, out _);
            rotationRoot.rotation = savedRotation;
            currentTopFace = savedFace;

            var baseY = ResolveSurfaceBaseWorldY(state, board, registry);
            return new Vector3(grid.x, baseY - minY + yOffset, grid.z);
        }

        public void PlayRoll(
            Direction direction,
            DiceState fromState,
            DiceState toState,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            PlayJumpRoll(direction, fromState, toState, 0f, board, registry, onComplete);
        }

        public void PlayJumpRoll(
            Direction direction,
            DiceState fromState,
            DiceState toState,
            float jumpYOffset,
            Board board,
            DiceRegistry registry,
            Action onComplete,
            bool fallBeforeSnap = false) {
            PlayJumpRoll(direction, fromState, toState, jumpYOffset, 1, board, registry, onComplete, fallBeforeSnap);
        }

        public void PlayJumpRoll(
            Direction direction,
            DiceState fromState,
            DiceState toState,
            float jumpYOffset,
            int rollDistance,
            Board board,
            DiceRegistry registry,
            Action onComplete,
            bool fallBeforeSnap = false,
            Func<VerticalMotionState> jumpMotionProvider = null) {
            if (!HasGameplaySettings()) {
                return;
            }

            if (erasureCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(JumpRollCoroutine(
                direction,
                fromState,
                toState,
                jumpYOffset,
                rollDistance,
                fallBeforeSnap,
                board,
                registry,
                onComplete,
                jumpMotionProvider));
        }

        public void PlayTransition(
            DiceTransition transition,
            Board board,
            DiceRegistry registry,
            Action onComplete,
            int slideCellDistance = 1) {
            if (erasureCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(
                TransitionCoroutine(transition, board, registry, onComplete, slideCellDistance));
        }

        public void PlayTransition(
            DiceTransition transition,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            PlayTransition(transition, board, registry, onComplete, 1);
        }

        public void PlaySpawnAppear(
            DiceState state,
            Board board,
            DiceRegistry registry,
            float spawnHeightAboveSurface,
            float bounceRestitution,
            int maxBounceCount,
            float minBounceVelocity,
            Action onComplete) {
            PlaySpawnAppear(
                state,
                board,
                registry,
                spawnHeightAboveSurface,
                bounceRestitution,
                maxBounceCount,
                minBounceVelocity,
                DiceBehaviorConstants.DefaultSpawnGravityScale,
                onComplete);
        }

        public void PlaySpawnAppear(
            DiceState state,
            Board board,
            DiceRegistry registry,
            float spawnHeightAboveSurface,
            float bounceRestitution,
            int maxBounceCount,
            float minBounceVelocity,
            float spawnGravityScale,
            Action onComplete) {
            if (!HasGameplaySettings()) {
                return;
            }

            if (erasureCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(SpawnAppearCoroutine(
                state,
                board,
                registry,
                spawnHeightAboveSurface,
                bounceRestitution,
                maxBounceCount,
                minBounceVelocity,
                spawnGravityScale,
                onComplete));
        }

        public void PlayBottomEmergenceAppear(
            DiceState state,
            Board board,
            DiceRegistry registry,
            float emergenceDuration,
            Action onComplete) {
            if (!HasGameplaySettings()) {
                return;
            }

            if (erasureCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(BottomEmergenceCoroutine(
                state,
                board,
                registry,
                emergenceDuration,
                onComplete));
        }

        public void SyncStackedSurface(DiceState state, Board board, DiceRegistry registry) {
            if (isAnimating || erasureProgress > 0f || board == null || registry == null) {
                return;
            }

            UpdateSurfaceBase(state, board, registry);
            ApplyErasureVisual(board, 0f);
        }

        public void SetCarryWorldPosition(Vector3 worldPosition) {
            if (positionRoot == null) {
                return;
            }

            positionRoot.SetParent(transform, true);
            positionRoot.position = worldPosition;
        }

        public void ApplyVisualYOffset(Board board, float offset) {
            visualYOffset = offset;
            if (board != null && !isAnimating && erasureProgress <= 0f) {
                ApplyErasureVisual(board, 0f);
            }
        }

        public void ClearVisualYOffset(Board board) {
            if (visualYOffset <= 0f) {
                return;
            }

            visualYOffset = 0f;
            if (board != null && !isAnimating && erasureProgress <= 0f) {
                ApplyErasureVisual(board, 0f);
            }
        }

        public void PlayErasure(
            ErasureKind kind,
            Board board,
            int topFace,
            Color? emissionColorOverride,
            Action onComplete) {
            if (kind == ErasureKind.None) {
                Debug.LogError("DiceView: PlayErasure requires Sink or Radiance.");
                onComplete?.Invoke();
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }

            if (erasureCoroutine != null) {
                StopCoroutine(erasureCoroutine);
            }

            activeErasureKind = kind;
            erasureEmissionColorOverride = emissionColorOverride;
            currentTopFace = topFace;
            erasureBoard = board;
            erasureCoroutine = StartCoroutine(ErasureCoroutine(board, kind, onComplete));
        }

        public void SetErasureEmissionColor(Color emissionColor) {
            erasureEmissionColorOverride = emissionColor;
            if (erasureProgress > 0f) {
                ApplyErasureEmission(erasureProgress);
            }
        }

        public void RetreatErasure(float amount) {
            if (erasureBoard == null || activeErasureKind == ErasureKind.None) {
                return;
            }

            erasureProgress = Mathf.Max(0f, erasureProgress - amount);
            ApplyErasureVisual(erasureBoard, erasureProgress);
        }

        public void AdvanceErasure(float amount) {
            if (erasureBoard == null || activeErasureKind == ErasureKind.None) {
                return;
            }

            erasureProgress = Mathf.Min(1f, erasureProgress + amount);
            ApplyErasureVisual(erasureBoard, erasureProgress);
        }

        public void CancelErasure() {
            if (erasureCoroutine != null) {
                StopCoroutine(erasureCoroutine);
                erasureCoroutine = null;
            }

            isAnimating = false;
            activeErasureKind = ErasureKind.None;
            erasureBoard = null;
        }

        public void PlayOneVanish(DiceOneVanishSettings settings, Action onComplete) {
            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }

            if (erasureCoroutine != null) {
                StopCoroutine(erasureCoroutine);
                erasureCoroutine = null;
            }

            if (oneVanishCoroutine != null) {
                StopCoroutine(oneVanishCoroutine);
            }

            EnsureMesh();
            oneVanishCoroutine = StartCoroutine(OneVanishCoroutine(settings, onComplete));
        }

        public void InterruptRollAnimation() {
            TryInterruptRollAnimation(out _);
        }

        public bool TryInterruptRollAnimation(out DiceRollVisualSnapshot snapshot) {
            snapshot = DiceRollVisualSnapshot.Invalid;
            if (rollCoroutine == null && !isAnimating) {
                return false;
            }

            if (positionRoot != null && rotationRoot != null) {
                snapshot = new DiceRollVisualSnapshot {
                    WorldPosition = positionRoot.position,
                    Rotation = rotationRoot.rotation,
                    IsValid = true
                };
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }

            DetachFromActiveRollPivot(snapshot);
            isAnimating = false;
            visualYOffset = 0f;
            return snapshot.IsValid;
        }

        void DetachFromActiveRollPivot(DiceRollVisualSnapshot snapshot) {
            if (positionRoot == null) {
                return;
            }

            var pivotParent = positionRoot.parent;
            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;

            if (snapshot.IsValid) {
                positionRoot.position = snapshot.WorldPosition;
                if (rotationRoot != null) {
                    rotationRoot.rotation = snapshot.Rotation;
                }
            }

            if (pivotParent != null
                && pivotParent != transform
                && pivotParent.name == "RollPivot") {
                Destroy(pivotParent.gameObject);
            }
        }

        public float GetJumpParallelRollDuration(int distance) {
            return animationSettings != null
                ? animationSettings.GetJumpParallelRollDuration(distance) * GetRollDurationMultiplier()
                : 0f;
        }

        public void PlayCancelGroundRollVisual(
            DiceRollVisualSnapshot snapshot,
            DiceState toState,
            float cancelProgress,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            if (!HasGameplaySettings() || !snapshot.IsValid) {
                onComplete?.Invoke();
                return;
            }

            if (erasureCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            var duration = Mathf.Clamp01(cancelProgress) * animationSettings.RollAnimationDuration * GetRollDurationMultiplier();
            rollCoroutine = StartCoroutine(CancelGroundRollCoroutine(
                snapshot,
                toState,
                duration,
                board,
                registry,
                onComplete));
        }

        public void PlayCancelJumpParallelRollVisual(
            DiceRollVisualSnapshot snapshot,
            DiceGridMovePlan plan,
            Board board,
            DiceRegistry registry,
            Action onComplete,
            Func<VerticalMotionState> jumpMotionProvider) {
            if (!HasGameplaySettings() || !snapshot.IsValid || jumpMotionProvider == null) {
                onComplete?.Invoke();
                return;
            }

            if (erasureCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(CancelJumpParallelRollCoroutine(
                snapshot,
                plan,
                board,
                registry,
                onComplete,
                jumpMotionProvider));
        }

        void UpdateSurfaceBase(DiceState state, Board board, DiceRegistry registry) {
            surfaceBaseWorldY = ResolveSurfaceBaseWorldY(state, board, registry);
        }

        static float ResolveSurfaceBaseWorldY(DiceState state, Board board, DiceRegistry registry) {
            var baseY = board.FloorSurfaceWorldY;
            if (state.Tier == DiceStackTier.Top
                && registry != null
                && registry.TryGetBottomAt(state.GridPos, out var bottom)
                && bottom != null) {
                baseY = bottom.GetTopSurfaceWorldY();
            }

            return baseY;
        }

        static DiceState BuildParallelRollStepState(DiceState fromState, Direction direction, int step) {
            var orientation = fromState.Orientation;
            for (var i = 0; i < step; i++) {
                orientation = orientation.Roll(direction);
            }

            return new DiceState(
                fromState.GridPos + direction.ToGridDelta() * step,
                orientation,
                fromState.Tier,
                fromState.Kind);
        }

        void CommitGridPlacement(
            DiceState state,
            Board board,
            DiceRegistry registry,
            float? preserveWorldY = null) {
            if (board == null || positionRoot == null || rotationRoot == null) {
                return;
            }

            gridWorldPosition = board.GridToWorld(state.GridPos);
            currentTopFace = state.Orientation.Top;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(state.Orientation);
            UpdateSurfaceBase(state, board, registry);
            ApplyErasureVisual(board, erasureProgress);

            if (preserveWorldY.HasValue) {
                var position = positionRoot.position;
                positionRoot.position = new Vector3(position.x, preserveWorldY.Value, position.z);
            }
        }

        IEnumerator JumpRollCoroutine(
            Direction direction,
            DiceState fromState,
            DiceState toState,
            float jumpYOffset,
            int rollDistance,
            bool fallBeforeSnap,
            Board board,
            DiceRegistry registry,
            Action onComplete,
            Func<VerticalMotionState> jumpMotionProvider = null) {
            isAnimating = true;
            EnsureMesh();
            if (positionRoot == null || rotationRoot == null) {
                isAnimating = false;
                onComplete?.Invoke();
                yield break;
            }

            currentTopFace = fromState.Orientation.Top;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(fromState.Orientation);

            var rolls = Mathf.Clamp(rollDistance, 1, DiceGridRollLimits.MaxParallelRollDistance);
            var useArcRoll = jumpMotionProvider != null;
            if (useArcRoll) {
                visualYOffset = 0f;
                yield return JumpArcRollCoroutine(
                    direction,
                    fromState,
                    toState,
                    rolls,
                    jumpMotionProvider,
                    board,
                    registry);
            } else {
                groundRollProgress = 0f;
                for (var i = 0; i < rolls; i++) {
                    yield return RollPhaseCoroutine(direction, board);
                    var stepState = BuildParallelRollStepState(fromState, direction, i + 1);
                    CommitGridPlacement(stepState, board, registry, preserveWorldY: positionRoot.position.y);
                }
            }

            yield return FinalizeJumpRollPlacement(
                fromState,
                toState,
                jumpYOffset,
                fallBeforeSnap,
                board,
                registry,
                skipHorizontalAlign: useArcRoll,
                skipJumpVisualAfterSnap: useArcRoll);

            onComplete?.Invoke();
        }

        IEnumerator JumpArcRollCoroutine(
            Direction direction,
            DiceState fromState,
            DiceState toState,
            int rollCount,
            Func<VerticalMotionState> jumpMotionProvider,
            Board board,
            DiceRegistry registry,
            Quaternion? rotationStartOverride = null) {
            if (positionRoot == null || rotationRoot == null || board == null || jumpMotionProvider == null) {
                yield break;
            }

            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;

            var jumpHeight = board.CellSize * physicsSettings.JumpHeightDiceMultiplier;
            var launchVelocityY = GravityMotion.ComputeLaunchVelocity(jumpHeight, physicsSettings.Gravity);
            var startWorld = positionRoot.position;
            var endGrid = board.GridToWorld(toState.GridPos);
            var fromBaseY = ResolveSurfaceBaseWorldY(fromState, board, registry);
            var toBaseY = ResolveSurfaceBaseWorldY(toState, board, registry);

            var midOrientation = fromState.Orientation.Roll(direction);
            var orientFrom = rotationStartOverride ?? DiceOrientationMapper.ToRotation(fromState.Orientation);
            var orientMid = DiceOrientationMapper.ToRotation(midOrientation);
            var orientTo = DiceOrientationMapper.ToRotation(toState.Orientation);
            var useSplitRotation = rollCount >= 2;

            var startMotion = jumpMotionProvider();
            var jumpTimelineAtRollStart = GravityMotion.ComputeFullJumpTimeline(
                startMotion,
                launchVelocityY,
                jumpHeight);

            while (true) {
                var motion = jumpMotionProvider();
                if (motion.IsGrounded) {
                    break;
                }

                var jumpTimeline = GravityMotion.ComputeFullJumpTimeline(motion, launchVelocityY, jumpHeight);
                var rollT = GravityMotion.ComputeRollArcProgress(jumpTimeline, jumpTimelineAtRollStart);
                var smoothT = Mathf.SmoothStep(0f, 1f, rollT);

                var xz = Vector2.Lerp(
                    new Vector2(startWorld.x, startWorld.z),
                    new Vector2(endGrid.x, endGrid.z),
                    smoothT);

                var rotation = useSplitRotation
                    ? rollT <= 0.5f
                        ? Quaternion.Slerp(orientFrom, orientMid, rollT * 2f)
                        : Quaternion.Slerp(orientMid, orientTo, (rollT - 0.5f) * 2f)
                    : Quaternion.Slerp(orientFrom, orientTo, smoothT);
                rotationRoot.rotation = rotation;

                ComputeVerticalExtents(board, currentTopFace, 1f, out var minY, out _);
                var baseY = Mathf.Lerp(fromBaseY, toBaseY, smoothT);
                positionRoot.position = new Vector3(xz.x, baseY - minY + motion.Offset, xz.y);
                yield return null;
            }

            rotationRoot.rotation = orientTo;
            currentTopFace = toState.Orientation.Top;
            gridWorldPosition = endGrid;
            UpdateSurfaceBase(toState, board, registry);
            ApplyErasureVisual(board, erasureProgress);

            ComputeVerticalExtents(board, currentTopFace, 1f, out var landedMinY, out _);
            positionRoot.position = new Vector3(endGrid.x, toBaseY - landedMinY, endGrid.z);
        }

        IEnumerator CancelJumpParallelRollCoroutine(
            DiceRollVisualSnapshot snapshot,
            DiceGridMovePlan plan,
            Board board,
            DiceRegistry registry,
            Action onComplete,
            Func<VerticalMotionState> jumpMotionProvider) {
            isAnimating = true;
            EnsureMesh();
            if (positionRoot == null || rotationRoot == null || board == null) {
                isAnimating = false;
                onComplete?.Invoke();
                yield break;
            }

            PrepareCancelJumpRollStart(snapshot, plan.From, board, registry);

            yield return JumpArcRollCoroutine(
                plan.Direction,
                plan.From,
                plan.To,
                plan.Distance,
                jumpMotionProvider,
                board,
                registry,
                snapshot.Rotation);

            rollCoroutine = null;
            SnapTo(plan.To, board, registry);
            onComplete?.Invoke();
        }

        void PrepareCancelJumpRollStart(
            DiceRollVisualSnapshot snapshot,
            DiceState fromState,
            Board board,
            DiceRegistry registry) {
            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            currentTopFace = fromState.Orientation.Top;
            positionRoot.position = snapshot.WorldPosition;
            rotationRoot.rotation = snapshot.Rotation;
        }

        IEnumerator CancelGroundRollCoroutine(
            DiceRollVisualSnapshot snapshot,
            DiceState toState,
            float duration,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            isAnimating = true;
            EnsureMesh();
            if (positionRoot == null || rotationRoot == null) {
                isAnimating = false;
                onComplete?.Invoke();
                yield break;
            }

            PrepareCancelRollStart(snapshot);
            currentTopFace = toState.Orientation.Top;
            var targetWorld = ComputeAnchoredWorldPosition(toState, board, registry, 0f);
            var targetRotation = DiceOrientationMapper.ToRotation(toState.Orientation);

            yield return AnimateCancelRollTransform(
                snapshot.WorldPosition,
                snapshot.Rotation,
                targetWorld,
                targetRotation,
                GetSafeCancelDuration(duration));

            rollCoroutine = null;
            SnapTo(toState, board, registry);
            onComplete?.Invoke();
        }

        void PrepareCancelRollStart(DiceRollVisualSnapshot snapshot) {
            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            positionRoot.position = snapshot.WorldPosition;
            rotationRoot.rotation = snapshot.Rotation;
        }

        static float GetSafeCancelDuration(float duration) {
            return Mathf.Max(duration, 0.001f);
        }

        IEnumerator AnimateCancelRollTransform(
            Vector3 fromWorld,
            Quaternion fromRotation,
            Vector3 toWorld,
            Quaternion toRotation,
            float duration) {
            var elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                positionRoot.position = Vector3.Lerp(fromWorld, toWorld, t);
                rotationRoot.rotation = Quaternion.Slerp(fromRotation, toRotation, t);
                yield return null;
            }

            positionRoot.position = toWorld;
            rotationRoot.rotation = toRotation;
        }

        IEnumerator FinalizeJumpRollPlacement(
            DiceState fromState,
            DiceState toState,
            float jumpYOffset,
            bool fallBeforeSnap,
            Board board,
            DiceRegistry registry,
            bool skipHorizontalAlign = false,
            bool skipJumpVisualAfterSnap = false) {
            CommitGridPlacement(toState, board, registry, preserveWorldY: positionRoot.position.y);

            if (fallBeforeSnap) {
                var targetWorld = ComputeAnchoredWorldPosition(toState, board, registry, visualYOffset);
                positionRoot.position = new Vector3(targetWorld.x, positionRoot.position.y, targetWorld.z);
                yield return AnimateGravityFall(targetWorld);
            } else if (!skipHorizontalAlign) {
                var targetWorld = ComputeAnchoredWorldPosition(toState, board, registry, visualYOffset);
                var rolled = new Vector3(targetWorld.x, positionRoot.position.y, targetWorld.z);
                if (Vector3.SqrMagnitude(rolled - positionRoot.position) > 0.0001f) {
                    yield return AnimatePositionLerp(
                        positionRoot.position,
                        rolled,
                        animationSettings.FallHorizontalDuration);
                    positionRoot.position = rolled;
                }
            }

            // SnapTo stops rollCoroutine; detach first so the caller can finish and invoke onComplete.
            rollCoroutine = null;
            SnapTo(toState, board, registry);

            if (fromState.Tier == DiceStackTier.Bottom && toState.Tier == DiceStackTier.Top) {
                ClearVisualYOffset(board);
            } else if (!skipJumpVisualAfterSnap
                && jumpYOffset > 0f
                && ShouldApplyJumpVisualYOffsetAfterSnap(fromState, toState)) {
                ApplyVisualYOffset(board, jumpYOffset);
            }
        }

        static bool ShouldApplyJumpVisualYOffsetAfterSnap(DiceState fromState, DiceState toState) {
            return toState.Tier != DiceStackTier.Top || fromState.Tier == DiceStackTier.Top;
        }

        IEnumerator TransitionCoroutine(
            DiceTransition transition,
            Board board,
            DiceRegistry registry,
            Action onComplete,
            int slideCellDistance = 1) {
            isAnimating = true;
            EnsureMesh();
            if (positionRoot == null) {
                isAnimating = false;
                onComplete?.Invoke();
                yield break;
            }

            if (transition.Path == DiceTransitionPath.FreeMove) {
                if (!transition.FromWorldOverride.HasValue || !transition.ToWorldOverride.HasValue) {
                    isAnimating = false;
                    onComplete?.Invoke();
                    yield break;
                }

                positionRoot.SetParent(transform, true);
                positionRoot.localRotation = Quaternion.identity;
                positionRoot.localScale = Vector3.one;

                var freeFrom = transition.FromWorldOverride.Value;
                var freeTo = transition.ToWorldOverride.Value;
                positionRoot.position = freeFrom;

                var freeMoveDuration = transition.SnapToGridOnComplete
                    ? animationSettings.PlaceDuration
                    : animationSettings.LiftDuration;
                yield return AnimatePositionLerp(freeFrom, freeTo, freeMoveDuration);
                positionRoot.position = freeTo;
            } else if (transition.Path == DiceTransitionPath.RollThenDrop) {
                if (rotationRoot == null) {
                    isAnimating = false;
                    onComplete?.Invoke();
                    yield break;
                }

                PrepareRollTransitionStart(transition.From, board, registry, transition.FromWorldOverride);
                yield return RollPhaseCoroutine(transition.RollDirection, board);
                yield return FinalizeJumpRollPlacement(
                    transition.From,
                    transition.To,
                    0f,
                    fallBeforeSnap: true,
                    board,
                    registry);
            } else if (transition.Path == DiceTransitionPath.RollThenRise) {
                if (rotationRoot == null) {
                    isAnimating = false;
                    onComplete?.Invoke();
                    yield break;
                }

                PrepareRollTransitionStart(transition.From, board, registry, transition.FromWorldOverride);
                yield return RollPhaseCoroutine(transition.RollDirection, board);

                currentTopFace = transition.To.Orientation.Top;
                rotationRoot.rotation = DiceOrientationMapper.ToRotation(transition.To.Orientation);

                var stackWorld = GetAnchoredWorldPosition(transition.To, board, registry);
                var rolled = new Vector3(stackWorld.x, positionRoot.position.y, stackWorld.z);
                if (Vector3.SqrMagnitude(rolled - positionRoot.position) > 0.0001f) {
                    yield return AnimatePositionLerp(positionRoot.position, rolled, animationSettings.FallHorizontalDuration);
                    positionRoot.position = rolled;
                }

                yield return AnimatePositionLerp(positionRoot.position, stackWorld, animationSettings.LiftDuration);
                positionRoot.position = stackWorld;
            } else {
                if (rotationRoot == null) {
                    isAnimating = false;
                    onComplete?.Invoke();
                    yield break;
                }

                PrepareGridTransition(transition.From);

                var fromWorld = transition.FromWorldOverride
                    ?? GetAnchoredWorldPosition(transition.From, board, registry);
                var toWorld = transition.ToWorldOverride
                    ?? GetAnchoredWorldPosition(transition.To, board, registry);
                positionRoot.position = fromWorld;

                if (transition.Path == DiceTransitionPath.Direct) {
                    var slideDuration = animationSettings.SlideDuration * Mathf.Max(1, slideCellDistance);
                    yield return AnimatePositionLerp(fromWorld, toWorld, slideDuration);
                } else {
                    var midWorld = new Vector3(toWorld.x, fromWorld.y, toWorld.z);
                    var horizontalDuration = animationSettings.FallHorizontalDuration * Mathf.Max(1, slideCellDistance);
                    yield return AnimatePositionLerp(fromWorld, midWorld, horizontalDuration);
                    positionRoot.position = midWorld;
                    yield return AnimateGravityFall(toWorld);
                    positionRoot.position = toWorld;
                }
            }

            if (transition.SnapToGridOnComplete && board != null) {
                SnapTo(transition.To, board, registry);
            }

            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }

        IEnumerator RollPhaseCoroutine(Direction direction, Board board) {
            if (positionRoot == null || board == null) {
                yield break;
            }

            groundRollProgress = 0f;
            var half = board.CellSize * 0.5f;
            var setup = DiceRollTransform.GetRollSetup(direction, half);

            var pivotObject = new GameObject("RollPivot");
            var pivot = pivotObject.transform;
            pivot.position = positionRoot.position + setup.PivotOffset;
            pivot.rotation = Quaternion.identity;

            positionRoot.SetParent(pivot, true);

            var elapsed = 0f;
            var rollDuration = animationSettings.RollAnimationDuration * GetRollDurationMultiplier();
            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var linearT = Mathf.Clamp01(elapsed / rollDuration);
                groundRollProgress = linearT;
                var t = Mathf.SmoothStep(0f, 1f, linearT);
                pivot.rotation = Quaternion.AngleAxis(setup.Angle * t, setup.Axis);
                yield return null;
            }

            groundRollProgress = 1f;
            pivot.rotation = Quaternion.AngleAxis(setup.Angle, setup.Axis);

            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            Destroy(pivotObject);
        }

        void PrepareRollTransitionStart(DiceState fromState, Board board, DiceRegistry registry, Vector3? fromWorldOverride) {
            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            currentTopFace = fromState.Orientation.Top;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(fromState.Orientation);

            if (fromWorldOverride.HasValue) {
                positionRoot.position = fromWorldOverride.Value;
                return;
            }

            SnapTo(fromState, board, registry);
        }

        void PrepareGridTransition(DiceState fromState) {
            positionRoot.SetParent(transform);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            currentTopFace = fromState.Orientation.Top;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(fromState.Orientation);
        }

        bool HasGameplaySettings() {
            if (physicsSettings != null && animationSettings != null && erasureSettings != null) {
                return true;
            }

            Debug.LogError("DiceView: Gameplay settings are not assigned. Configure via GameBootstrap.");
            return false;
        }

        IEnumerator AnimateGravityFall(Vector3 targetWorld) {
            if (positionRoot == null || !HasGameplaySettings()) {
                yield break;
            }

            var startOffset = positionRoot.position.y - targetWorld.y;
            var state = GravityMotion.CreateDrop(startOffset);
            yield return GravityMotion.AnimateVerticalDropCoroutine(
                state,
                physicsSettings.Gravity,
                targetWorld.y,
                () => positionRoot.position.x,
                () => positionRoot.position.z,
                (x, y, z) => positionRoot.position = new Vector3(x, y, z));
        }

        IEnumerator SpawnAppearCoroutine(
            DiceState state,
            Board board,
            DiceRegistry registry,
            float spawnHeightAboveSurface,
            float bounceRestitution,
            int maxBounceCount,
            float minBounceVelocity,
            float spawnGravityScale,
            Action onComplete) {
            isAnimating = true;
            EnsureMesh();
            if (dissolvePivot == null || rotationRoot == null || positionRoot == null) {
                isAnimating = false;
                rollCoroutine = null;
                onComplete?.Invoke();
                yield break;
            }

            erasureProgress = 0f;
            erasureBoard = null;
            wasErasureGhost = false;
            activeErasureKind = ErasureKind.None;
            visualYOffset = 0f;
            currentTopFace = state.Orientation.Top;
            positionRoot.SetParent(transform);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(state.Orientation);
            CommitGridPlacement(state, board, registry);
            ApplyErasureVisual(board, 0f);

            var landedWorld = positionRoot.position;
            var groundWorldY = landedWorld.y;
            positionRoot.position = landedWorld + Vector3.up * spawnHeightAboveSurface;

            var gravity = physicsSettings.Gravity
                * Mathf.Max(0.01f, spawnGravityScale);
            var motion = GravityMotion.CreateDrop(spawnHeightAboveSurface);
            yield return GravityMotion.AnimateSpawnBounceDropCoroutine(
                motion,
                gravity,
                groundWorldY,
                bounceRestitution,
                maxBounceCount,
                minBounceVelocity,
                () => positionRoot.position.x,
                () => positionRoot.position.z,
                (x, y, z) => positionRoot.position = new Vector3(x, y, z));

            SnapTo(state, board, registry);
            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }

        IEnumerator BottomEmergenceCoroutine(
            DiceState state,
            Board board,
            DiceRegistry registry,
            float emergenceDuration,
            Action onComplete) {
            isAnimating = true;
            EnsureMesh();
            if (dissolvePivot == null || rotationRoot == null || positionRoot == null) {
                isAnimating = false;
                rollCoroutine = null;
                onComplete?.Invoke();
                yield break;
            }

            wasErasureGhost = false;
            visualYOffset = 0f;
            currentTopFace = state.Orientation.Top;
            positionRoot.SetParent(transform);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(state.Orientation);
            CommitGridPlacement(state, board, registry);

            var duration = Mathf.Max(0.01f, emergenceDuration);
            var progress = 1f;
            ApplyEmergenceVisual(board, progress);

            while (progress > 0f) {
                progress = Mathf.Max(0f, progress - Time.deltaTime / duration);
                ApplyEmergenceVisual(board, progress);
                yield return null;
            }

            SnapTo(state, board, registry);
            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }

        IEnumerator AnimatePositionLerp(Vector3 fromWorld, Vector3 toWorld, float duration) {
            if (duration <= 0f) {
                positionRoot.position = toWorld;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                positionRoot.position = Vector3.Lerp(fromWorld, toWorld, t);
                yield return null;
            }

            positionRoot.position = toWorld;
        }

        IEnumerator OneVanishCoroutine(DiceOneVanishSettings settings, Action onComplete) {
            isAnimating = true;
            EnsureMesh();

            var elapsed = 0f;
            var vanishDuration = Mathf.Max(0.01f, settings.VanishDuration);
            while (elapsed < vanishDuration) {
                elapsed += Time.deltaTime;
                ApplyOneVanishEmission(settings, GetOneVanishEmissionFactor(elapsed, settings));
                yield return null;
            }

            ResetOneVanishVisuals();
            isAnimating = false;
            oneVanishCoroutine = null;
            onComplete?.Invoke();
        }

        static float GetOneVanishEmissionFactor(float elapsed, DiceOneVanishSettings settings) {
            var rampUpDuration = settings.RampUpDuration;
            if (rampUpDuration <= 0f) {
                return 1f;
            }

            if (elapsed < rampUpDuration) {
                return Mathf.Clamp01(elapsed / rampUpDuration);
            }

            return 1f;
        }

        void ApplyOneVanishEmission(DiceOneVanishSettings settings, float factor) {
            if (dissolveMaterials.Count == 0 || erasureSettings == null) {
                return;
            }

            if (factor <= 0f) {
                ResetOneVanishVisuals();
                return;
            }

            var baseColor = erasureEmissionColorOverride ?? erasureSettings.NeutralEmissionColor;
            var emissionColor = baseColor * (settings.EmissionIntensity * factor);

            for (var i = 0; i < dissolveMaterials.Count; i++) {
                var map = dissolveEmissionMapOverride != null
                    ? dissolveEmissionMapOverride
                    : erasureSettings.ErasureEmissionMap != null
                        ? erasureSettings.ErasureEmissionMap
                        : dissolveMaterialBaseEmissionMaps[i];
                SetMaterialEmission(dissolveMaterials[i], emissionColor, map, true);
            }
        }

        void ResetOneVanishVisuals() {
            if (dissolveMaterials.Count == 0) {
                erasureEmissionColorOverride = null;
                return;
            }

            for (var i = 0; i < dissolveMaterials.Count; i++) {
                RestoreMaterialEmission(
                    dissolveMaterials[i],
                    dissolveMaterialBaseEmissionColors[i],
                    dissolveMaterialBaseEmissionMaps[i],
                    dissolveMaterialHadEmission[i]);
            }

            erasureEmissionColorOverride = null;
        }

        IEnumerator ErasureCoroutine(Board board, ErasureKind kind, Action onComplete) {
            isAnimating = true;
            EnsureMesh();
            if (dissolvePivot == null || positionRoot == null || erasureSettings == null) {
                isAnimating = false;
                activeErasureKind = ErasureKind.None;
                onComplete?.Invoke();
                yield break;
            }

            positionRoot.SetParent(transform);

            var duration = kind == ErasureKind.Radiance
                ? erasureSettings.RadianceDuration
                : erasureSettings.SinkDuration;

            while (erasureProgress < 1f) {
                erasureProgress = Mathf.Min(1f, erasureProgress + Time.deltaTime / duration);
                ApplyErasureVisual(board, erasureProgress);
                yield return null;
            }

            erasureProgress = 1f;
            ApplyErasureVisual(board, erasureProgress);
            isAnimating = false;
            erasureCoroutine = null;
            erasureBoard = null;
            activeErasureKind = ErasureKind.None;
            onComplete?.Invoke();
        }

        void ApplyErasureVisual(Board board, float progress) {
            if (dissolvePivot == null || positionRoot == null || rotationRoot == null || board == null) {
                return;
            }

            erasureProgress = progress;
            if (activeErasureKind == ErasureKind.Radiance) {
                ApplyRadianceLayout(board);
                ApplyErasureEmission(GetRadianceEmissionFactor(progress));
                EnsurePushBody();
                pushBody?.SetCollisionEnabled(true);
                return;
            }

            ApplySurfaceLayout(board, progress);
            ApplySinkGhostVisual(progress);
            SyncStackedTopDuringErasure();
        }

        /// <summary>
        /// Spawn emergence only. Applies sink visuals without ghost gameplay side effects.
        /// </summary>
        void ApplyEmergenceVisual(Board board, float progress) {
            if (dissolvePivot == null || positionRoot == null || rotationRoot == null || board == null) {
                return;
            }

            erasureProgress = progress;
            ApplySurfaceLayout(board, progress);
            ApplyErasureAlpha(progress, allowGhostAlpha: false);
            ApplyErasureEmission(progress, useNeutralColor: true);
            EnsurePushBody();
            pushBody?.SetCollisionEnabled(progress < erasureSettings.SinkGhostThreshold);
        }

        void ApplyRadianceLayout(Board board) {
            ApplyMeshVisualScale(board);
            dissolvePivot.localScale = GetDissolveLocalScale(currentTopFace, 1f);
            ComputeVerticalExtents(board, currentTopFace, 1f, out var minY, out _);
            positionRoot.position = new Vector3(
                gridWorldPosition.x,
                surfaceBaseWorldY - minY + visualYOffset,
                gridWorldPosition.z);
        }

        float GetRadianceEmissionFactor(float progress) {
            if (erasureSettings == null) {
                return Mathf.Clamp01(progress);
            }

            var rampDuration = erasureSettings.RadianceRampUpDuration;
            var totalDuration = erasureSettings.RadianceDuration;
            if (rampDuration <= 0f || totalDuration <= 0f) {
                return 1f;
            }

            var elapsed = progress * totalDuration;
            if (elapsed < rampDuration) {
                return elapsed / rampDuration;
            }

            return 1f;
        }

        void ApplySurfaceLayout(Board board, float progress) {
            ApplyMeshVisualScale(board);
            var squash = 1f - progress;
            dissolvePivot.localScale = GetDissolveLocalScale(currentTopFace, squash);
            ComputeVerticalExtents(board, currentTopFace, squash, out var minY, out _);
            positionRoot.position = new Vector3(
                gridWorldPosition.x,
                surfaceBaseWorldY - minY + visualYOffset,
                gridWorldPosition.z);
        }

        void SyncStackedTopDuringErasure() {
            if (erasureProgress <= 0f || activeErasureKind != ErasureKind.Sink) {
                return;
            }

            EnsureDiceController();
            diceController?.NotifyStackedTopSync();
        }

        void ApplySinkGhostVisual(float progress) {
            ApplyErasureAlpha(progress, allowGhostAlpha: true);
            ApplyErasureEmission(progress);
            EnsurePushBody();
            pushBody?.SetCollisionEnabled(!IsErasureGhost);

            if (IsErasureGhost && !wasErasureGhost) {
                wasErasureGhost = true;
                EnsureDiceController();
                diceController?.OnBecameErasureGhost();
            } else if (!IsErasureGhost && wasErasureGhost) {
                wasErasureGhost = false;
                EnsureDiceController();
                diceController?.OnCeasedErasureGhost();
            }
        }

        void ResetErasureVisuals() {
            ApplyErasureAlpha(0f, allowGhostAlpha: true);
            ApplyErasureEmission(0f);
            erasureEmissionColorOverride = null;
            EnsurePushBody();
            pushBody?.SetCollisionEnabled(true);
            wasErasureGhost = false;
            activeErasureKind = ErasureKind.None;
        }

        void ApplyErasureEmission(float progress, bool useNeutralColor = false) {
            if (dissolveMaterials.Count == 0 || erasureSettings == null) {
                return;
            }

            if (progress <= 0f) {
                for (var i = 0; i < dissolveMaterials.Count; i++) {
                    RestoreMaterialEmission(
                        dissolveMaterials[i],
                        dissolveMaterialBaseEmissionColors[i],
                        dissolveMaterialBaseEmissionMaps[i],
                        dissolveMaterialHadEmission[i]);
                }

                return;
            }

            var pulse = (Mathf.Sin(Time.time * erasureSettings.ErasureEmissionPulseSpeed) + 1f) * 0.5f;
            var pulseMultiplier = Mathf.Lerp(
                erasureSettings.ErasureEmissionPulseMin,
                erasureSettings.ErasureEmissionPulseMax,
                pulse);
            var baseColor = useNeutralColor
                ? erasureSettings.NeutralEmissionColor
                : erasureEmissionColorOverride
                    ?? erasureSettings.NeutralEmissionColor;
            var emissionColor = baseColor
                * (erasureSettings.ErasureEmissionIntensity * pulseMultiplier);

            for (var i = 0; i < dissolveMaterials.Count; i++) {
                var map = dissolveEmissionMapOverride != null
                    ? dissolveEmissionMapOverride
                    : erasureSettings.ErasureEmissionMap != null
                        ? erasureSettings.ErasureEmissionMap
                        : dissolveMaterialBaseEmissionMaps[i];
                SetMaterialEmission(dissolveMaterials[i], emissionColor, map, true);
            }
        }

        void ApplyErasureAlpha(float progress, bool allowGhostAlpha) {
            if (dissolveMaterials.Count == 0 || erasureSettings == null) {
                return;
            }

            var useTransparent = allowGhostAlpha && progress >= erasureSettings.SinkGhostThreshold;
            if (useTransparent != dissolveMaterialsTransparent) {
                dissolveMaterialsTransparent = useTransparent;
                for (var i = 0; i < dissolveMaterials.Count; i++) {
                    SetMaterialSurfaceType(dissolveMaterials[i], useTransparent);
                }
            }

            var alpha = useTransparent ? erasureSettings.SinkGhostAlpha : 1f;

            for (var i = 0; i < dissolveMaterials.Count; i++) {
                var color = dissolveMaterialBaseColors[i];
                color.a = alpha;
                SetMaterialBaseColor(dissolveMaterials[i], color);
                if (useTransparent && dissolveEmissionMapOverride != null) {
                    SetMaterialBaseMap(dissolveMaterials[i], dissolveEmissionMapOverride);
                }
            }
        }

        float GetRollDurationMultiplier() {
            EnsureDiceController();
            return diceController != null
                ? diceController.Capabilities.RollDurationMultiplier
                : DiceBehaviorConstants.DefaultRollDurationMultiplier;
        }

        static Texture ResolveBaseMapFromPrefab(GameObject prefab) {
            if (prefab == null) {
                return null;
            }

            var renderer = prefab.GetComponentInChildren<Renderer>(true);
            if (renderer == null) {
                return null;
            }

            var material = renderer.sharedMaterial;
            if (material == null || !material.HasProperty("_BaseMap")) {
                return null;
            }

            return material.GetTexture("_BaseMap");
        }

        static void SetMaterialBaseMap(Material material, Texture texture) {
            if (material == null || texture == null) {
                return;
            }

            if (material.HasProperty("_BaseMap")) {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex")) {
                material.SetTexture("_MainTex", texture);
            }
        }

        static Color GetMaterialBaseColor(Material material) {
            if (material.HasProperty("_BaseColor")) {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color")) {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        static void SetMaterialBaseColor(Material material, Color color) {
            if (material.HasProperty("_BaseColor")) {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color")) {
                material.SetColor("_Color", color);
            }
        }

        static Color GetMaterialEmissionColor(Material material) {
            if (material.HasProperty("_EmissionColor")) {
                return material.GetColor("_EmissionColor");
            }

            return Color.black;
        }

        static Texture GetMaterialEmissionMap(Material material) {
            if (material.HasProperty("_EmissionMap")) {
                return material.GetTexture("_EmissionMap");
            }

            return null;
        }

        static void SetMaterialEmission(Material material, Color color, Texture map, bool enable) {
            if (!material.HasProperty("_EmissionColor")) {
                return;
            }

            if (!enable) {
                RestoreMaterialEmission(material, Color.black, null, false);
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
            if (material.HasProperty("_EmissionMap") && map != null) {
                material.SetTexture("_EmissionMap", map);
            }

            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static void RestoreMaterialEmission(Material material, Color color, Texture map, bool hadEmission) {
            if (!material.HasProperty("_EmissionColor")) {
                return;
            }

            if (!hadEmission) {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
            if (material.HasProperty("_EmissionMap")) {
                material.SetTexture("_EmissionMap", map);
            }

            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static void SetMaterialSurfaceType(Material material, bool transparent) {
            if (material.HasProperty("_Surface")) {
                material.SetFloat("_Surface", transparent ? 1f : 0f);
            }

            if (transparent) {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                return;
            }

            material.SetOverrideTag("RenderType", "Opaque");
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.renderQueue = -1;
            material.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        static Vector3 GetDissolveLocalScale(int topFace, float squash) {
            var axis = DiceOrientationMapper.FaceMeshNormal(topFace);
            return new Vector3(
                Mathf.Abs(axis.x) > 0.5f ? squash : 1f,
                Mathf.Abs(axis.y) > 0.5f ? squash : 1f,
                Mathf.Abs(axis.z) > 0.5f ? squash : 1f);
        }

        void ComputeVerticalExtents(Board board, int topFace, float squash, out float minY, out float maxY) {
            var halfSize = board.CellSize * 0.5f;
            var scale = GetDissolveLocalScale(topFace, squash);
            var rotation = rotationRoot.rotation;
            minY = float.PositiveInfinity;
            maxY = float.NegativeInfinity;

            for (var sx = -1; sx <= 1; sx += 2) {
                for (var sy = -1; sy <= 1; sy += 2) {
                    for (var sz = -1; sz <= 1; sz += 2) {
                        var local = Vector3.Scale(new Vector3(sx, sy, sz) * halfSize, scale);
                        var worldY = (rotation * local).y;
                        minY = Mathf.Min(minY, worldY);
                        maxY = Mathf.Max(maxY, worldY);
                    }
                }
            }
        }
    }
}
