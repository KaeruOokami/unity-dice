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
        DiceDissolveSettings dissolveSettings;

        Transform meshInstance;
        Coroutine rollCoroutine;
        Coroutine dissolveCoroutine;
        bool isAnimating;
        float dissolveProgress;
        int currentTopFace = 1;
        Vector3 gridWorldPosition;
        float surfaceBaseWorldY;
        float visualYOffset;
        Board dissolveBoard;
        DicePushBody pushBody;
        DiceController diceController;
        bool wasDissolveGhost;
        readonly List<Material> dissolveMaterials = new();
        readonly List<Color> dissolveMaterialBaseColors = new();
        readonly List<Color> dissolveMaterialBaseEmissionColors = new();
        readonly List<Texture> dissolveMaterialBaseEmissionMaps = new();
        readonly List<bool> dissolveMaterialHadEmission = new();
        bool dissolveMaterialsTransparent;

        public bool IsAnimating => isAnimating;
        public float DissolveProgress => dissolveProgress;
        public bool IsDissolveGhost =>
            dissolveSettings != null && dissolveProgress >= dissolveSettings.DissolveGhostThreshold;

        public Transform DiceTransform => positionRoot;

        public void Configure(
            PhysicsSettings physics,
            DiceAnimationSettings animation,
            DiceDissolveSettings dissolve) {
            physicsSettings = physics;
            animationSettings = animation;
            dissolveSettings = dissolve;
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
            CacheDissolveMaterials();
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

            ComputeVerticalExtents(board, currentTopFace, 1f - dissolveProgress, out _, out var maxY);
            return positionRoot.position.y + maxY;
        }

        public float GetLogicalTopSurfaceWorldY(Board board) {
            if (board == null || rotationRoot == null) {
                return 0f;
            }

            var squash = 1f - dissolveProgress;
            ComputeVerticalExtents(board, currentTopFace, squash, out var minY, out var maxY);
            return surfaceBaseWorldY - minY + maxY;
        }

        public void SnapTo(DiceState state, Board board, DiceRegistry registry = null) {
            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }

            if (dissolveCoroutine != null) {
                StopCoroutine(dissolveCoroutine);
                dissolveCoroutine = null;
            }

            isAnimating = false;
            dissolveProgress = 0f;
            dissolveBoard = null;
            wasDissolveGhost = false;
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
            ResetDissolveVisuals();
            ApplySurfaceVisual(board, 0f);
        }

        public Vector3 GetAnchoredWorldPosition(DiceState state, Board board, DiceRegistry registry) {
            EnsureMesh();
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
            ComputeVerticalExtents(board, state.Orientation.Top, 1f - dissolveProgress, out var minY, out _);
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

            if (dissolveCoroutine != null) {
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
            Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(TransitionCoroutine(transition, board, registry, onComplete));
        }

        public void SyncStackedSurface(DiceState state, Board board, DiceRegistry registry) {
            if (isAnimating || dissolveProgress > 0f || board == null || registry == null) {
                return;
            }

            UpdateSurfaceBase(state, board, registry);
            ApplySurfaceVisual(board, 0f);
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
            if (board != null && !isAnimating && dissolveProgress <= 0f) {
                ApplySurfaceVisual(board, 0f);
            }
        }

        public void ClearVisualYOffset(Board board) {
            if (visualYOffset <= 0f) {
                return;
            }

            visualYOffset = 0f;
            if (board != null && !isAnimating && dissolveProgress <= 0f) {
                ApplySurfaceVisual(board, 0f);
            }
        }

        public void PlayDissolve(Board board, int topFace, Action onComplete) {
            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }

            if (dissolveCoroutine != null) {
                StopCoroutine(dissolveCoroutine);
            }

            currentTopFace = topFace;
            dissolveBoard = board;
            dissolveCoroutine = StartCoroutine(DissolveCoroutine(board, onComplete));
        }

        public void RetreatDissolve(float amount) {
            if (dissolveBoard == null) {
                return;
            }

            dissolveProgress = Mathf.Max(0f, dissolveProgress - amount);
            ApplySurfaceVisual(dissolveBoard, dissolveProgress);
        }

        public void CancelDissolve() {
            if (dissolveCoroutine != null) {
                StopCoroutine(dissolveCoroutine);
                dissolveCoroutine = null;
            }

            isAnimating = false;
            dissolveBoard = null;
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

            return new DiceState(fromState.GridPos + direction.ToGridDelta() * step, orientation, fromState.Tier);
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
            ApplySurfaceVisual(board, dissolveProgress);

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

            var rolls = Mathf.Clamp(rollDistance, 1, RollResolver.MaxParallelRollDistance);
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
            DiceRegistry registry) {
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
            var orientFrom = DiceOrientationMapper.ToRotation(fromState.Orientation);
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
            ApplySurfaceVisual(board, dissolveProgress);

            ComputeVerticalExtents(board, currentTopFace, 1f, out var landedMinY, out _);
            positionRoot.position = new Vector3(endGrid.x, toBaseY - landedMinY, endGrid.z);
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
            Action onComplete) {
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
                    yield return AnimatePositionLerp(fromWorld, toWorld, animationSettings.SlideDuration);
                } else {
                    var midWorld = new Vector3(toWorld.x, fromWorld.y, toWorld.z);
                    yield return AnimatePositionLerp(fromWorld, midWorld, animationSettings.FallHorizontalDuration);
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

            var half = board.CellSize * 0.5f;
            var setup = DiceRollTransform.GetRollSetup(direction, half);

            var pivotObject = new GameObject("RollPivot");
            var pivot = pivotObject.transform;
            pivot.position = positionRoot.position + setup.PivotOffset;
            pivot.rotation = Quaternion.identity;

            positionRoot.SetParent(pivot, true);

            var elapsed = 0f;
            while (elapsed < animationSettings.RollAnimationDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / animationSettings.RollAnimationDuration);
                pivot.rotation = Quaternion.AngleAxis(setup.Angle * t, setup.Axis);
                yield return null;
            }

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
            if (physicsSettings != null && animationSettings != null && dissolveSettings != null) {
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

        IEnumerator DissolveCoroutine(Board board, Action onComplete) {
            isAnimating = true;
            EnsureMesh();
            if (dissolvePivot == null || positionRoot == null) {
                isAnimating = false;
                onComplete?.Invoke();
                yield break;
            }

            positionRoot.SetParent(transform);

            while (dissolveProgress < 1f) {
                dissolveProgress = Mathf.Min(1f, dissolveProgress + Time.deltaTime / dissolveSettings.DissolveDuration);
                ApplySurfaceVisual(board, dissolveProgress);
                yield return null;
            }

            dissolveProgress = 1f;
            ApplySurfaceVisual(board, dissolveProgress);
            isAnimating = false;
            dissolveCoroutine = null;
            dissolveBoard = null;
            onComplete?.Invoke();
        }

        void ApplySurfaceVisual(Board board, float progress) {
            if (dissolvePivot == null || positionRoot == null || rotationRoot == null || board == null) {
                return;
            }

            var squash = 1f - progress;
            dissolvePivot.localScale = GetDissolveLocalScale(currentTopFace, squash);
            ComputeVerticalExtents(board, currentTopFace, squash, out var minY, out _);
            positionRoot.position = new Vector3(
                gridWorldPosition.x,
                surfaceBaseWorldY - minY + visualYOffset,
                gridWorldPosition.z);
            ApplyDissolveGhostVisual(progress);
            SyncStackedTopDuringDissolve();
        }

        void SyncStackedTopDuringDissolve() {
            if (dissolveProgress <= 0f) {
                return;
            }

            EnsureDiceController();
            diceController?.NotifyStackedTopSync();
        }

        void ApplyDissolveGhostVisual(float progress) {
            ApplyDissolveAlpha(progress);
            ApplyDissolveEmission(progress);
            EnsurePushBody();
            pushBody?.SetCollisionEnabled(!IsDissolveGhost);

            if (IsDissolveGhost && !wasDissolveGhost) {
                wasDissolveGhost = true;
                EnsureDiceController();
                diceController?.OnBecameDissolveGhost();
            } else if (!IsDissolveGhost && wasDissolveGhost) {
                wasDissolveGhost = false;
                EnsureDiceController();
                diceController?.OnCeasedDissolveGhost();
            }
        }

        void ResetDissolveVisuals() {
            ApplyDissolveAlpha(0f);
            ApplyDissolveEmission(0f);
            EnsurePushBody();
            pushBody?.SetCollisionEnabled(true);
            wasDissolveGhost = false;
        }

        void ApplyDissolveEmission(float progress) {
            if (dissolveMaterials.Count == 0) {
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

            var pulse = (Mathf.Sin(Time.time * dissolveSettings.DissolveEmissionPulseSpeed) + 1f) * 0.5f;
            var pulseMultiplier = Mathf.Lerp(
                dissolveSettings.DissolveEmissionPulseMin,
                dissolveSettings.DissolveEmissionPulseMax,
                pulse);
            var emissionColor = dissolveSettings.DissolveEmissionColor
                * (dissolveSettings.DissolveEmissionIntensity * pulseMultiplier);

            for (var i = 0; i < dissolveMaterials.Count; i++) {
                var map = dissolveSettings.DissolveEmissionMap != null
                    ? dissolveSettings.DissolveEmissionMap
                    : dissolveMaterialBaseEmissionMaps[i];
                SetMaterialEmission(dissolveMaterials[i], emissionColor, map, true);
            }
        }

        void ApplyDissolveAlpha(float progress) {
            if (dissolveMaterials.Count == 0) {
                return;
            }

            var useTransparent = progress >= dissolveSettings.DissolveGhostThreshold;
            if (useTransparent != dissolveMaterialsTransparent) {
                dissolveMaterialsTransparent = useTransparent;
                for (var i = 0; i < dissolveMaterials.Count; i++) {
                    SetMaterialSurfaceType(dissolveMaterials[i], useTransparent);
                }
            }

            var alpha = useTransparent ? dissolveSettings.DissolveGhostAlpha : 1f;

            for (var i = 0; i < dissolveMaterials.Count; i++) {
                var color = dissolveMaterialBaseColors[i];
                color.a = alpha;
                SetMaterialBaseColor(dissolveMaterials[i], color);
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
