using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
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
        [SerializeField] float rollDuration = 0.3f;
        [SerializeField] float dissolveDuration = 0.8f;
        [SerializeField] float dissolveGhostThreshold = 0.45f;
        [SerializeField] float dissolveGhostAlpha = 0.35f;

        Transform meshInstance;
        Coroutine rollCoroutine;
        Coroutine dissolveCoroutine;
        bool isAnimating;
        float dissolveProgress;
        int currentTopFace = 1;
        Vector3 gridWorldPosition;
        float surfaceBaseWorldY;
        Board dissolveBoard;
        DicePushBody pushBody;
        readonly List<Material> dissolveMaterials = new();
        readonly List<Color> dissolveMaterialBaseColors = new();
        bool dissolveMaterialsTransparent;

        public bool IsAnimating => isAnimating;
        public float DissolveProgress => dissolveProgress;
        public bool IsDissolveGhost => dissolveProgress >= dissolveGhostThreshold;

        public Transform DiceTransform => positionRoot;

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

        void CacheDissolveMaterials() {
            dissolveMaterials.Clear();
            dissolveMaterialBaseColors.Clear();
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
            if (positionRoot == null || rotationRoot == null || board == null) {
                return Vector3.zero;
            }

            var savedGrid = gridWorldPosition;
            var savedFace = currentTopFace;
            var savedBase = surfaceBaseWorldY;
            var savedRotation = rotationRoot.rotation;

            gridWorldPosition = board.GridToWorld(state.GridPos);
            currentTopFace = state.Orientation.Top;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(state.Orientation);
            UpdateSurfaceBase(state, board, registry);
            ApplySurfaceVisual(board, dissolveProgress);

            var worldPosition = positionRoot.position;

            gridWorldPosition = savedGrid;
            currentTopFace = savedFace;
            surfaceBaseWorldY = savedBase;
            rotationRoot.rotation = savedRotation;
            ApplySurfaceVisual(board, dissolveProgress);

            return worldPosition;
        }

        public void PlayRoll(
            Direction direction,
            DiceState fromState,
            DiceState toState,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(RollCoroutine(direction, fromState, toState, board, registry, onComplete));
        }

        public void PlaySlide(DiceState fromState, DiceState toState, Board board, DiceRegistry registry, Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(SlideCoroutine(fromState, toState, board, registry, onComplete));
        }

        public void PlayStackMove(
            DiceState fromState,
            DiceState toState,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(StackMoveCoroutine(fromState, toState, board, registry, onComplete));
        }

        public void PlayStackMoveFallToBottom(
            DiceState fromState,
            DiceState toState,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(StackMoveFallCoroutine(fromState, toState, board, registry, onComplete));
        }

        public void PlayLift(Vector3 fromWorld, Vector3 toWorld, Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(LiftPlaceCoroutine(fromWorld, toWorld, false, default, null, null, onComplete));
        }

        public void PlayPlace(
            Vector3 fromWorld,
            Vector3 toWorld,
            DiceState toState,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(LiftPlaceCoroutine(fromWorld, toWorld, true, toState, board, registry, onComplete));
        }

        public void SetCarryWorldPosition(Vector3 worldPosition) {
            if (positionRoot == null) {
                return;
            }

            positionRoot.SetParent(transform, true);
            positionRoot.position = worldPosition;
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

        void UpdateSurfaceBase(DiceState state, Board board, DiceRegistry registry) {
            surfaceBaseWorldY = board.FloorSurfaceWorldY;
            if (state.Tier == DiceStackTier.Top
                && registry != null
                && registry.TryGetBottomAt(state.GridPos, out var bottom)
                && bottom != null) {
                surfaceBaseWorldY = bottom.GetTopSurfaceWorldY();
            }
        }

        IEnumerator RollCoroutine(
            Direction direction,
            DiceState fromState,
            DiceState toState,
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

            SnapTo(fromState, board, registry);

            var half = board.CellSize * 0.5f;
            var setup = DiceRollTransform.GetRollSetup(direction, half);
            var diceCenter = board.GridToWorld(fromState.GridPos);

            var pivotObject = new GameObject("RollPivot");
            var pivot = pivotObject.transform;
            pivot.position = diceCenter + setup.PivotOffset;
            pivot.rotation = Quaternion.identity;

            positionRoot.SetParent(pivot, true);

            var elapsed = 0f;
            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / rollDuration);
                pivot.rotation = Quaternion.AngleAxis(setup.Angle * t, setup.Axis);
                yield return null;
            }

            pivot.rotation = Quaternion.AngleAxis(setup.Angle, setup.Axis);

            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            Destroy(pivotObject);

            SnapTo(toState, board, registry);

            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }

        IEnumerator SlideCoroutine(
            DiceState fromState,
            DiceState toState,
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

            positionRoot.SetParent(transform);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            currentTopFace = fromState.Orientation.Top;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(fromState.Orientation);

            var fromWorld = GetAnchoredWorldPosition(fromState, board, registry);
            var toWorld = GetAnchoredWorldPosition(toState, board, registry);
            positionRoot.position = fromWorld;

            var elapsed = 0f;
            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / rollDuration);
                positionRoot.position = Vector3.Lerp(fromWorld, toWorld, t);
                yield return null;
            }

            SnapTo(toState, board, registry);

            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }

        IEnumerator StackMoveCoroutine(
            DiceState fromState,
            DiceState toState,
            Board board,
            DiceRegistry registry,
            Action onComplete) {
            yield return SlideCoroutine(fromState, toState, board, registry, onComplete);
        }

        IEnumerator StackMoveFallCoroutine(
            DiceState fromState,
            DiceState toState,
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

            positionRoot.SetParent(transform);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            currentTopFace = fromState.Orientation.Top;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(fromState.Orientation);

            var fromWorld = GetAnchoredWorldPosition(fromState, board, registry);
            var toWorld = GetAnchoredWorldPosition(toState, board, registry);
            var midWorld = new Vector3(toWorld.x, fromWorld.y, toWorld.z);
            positionRoot.position = fromWorld;

            var elapsed = 0f;
            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / rollDuration);
                positionRoot.position = Vector3.Lerp(fromWorld, midWorld, t);
                yield return null;
            }

            positionRoot.position = midWorld;

            elapsed = 0f;
            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / rollDuration);
                positionRoot.position = Vector3.Lerp(midWorld, toWorld, t);
                yield return null;
            }

            SnapTo(toState, board, registry);

            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }

        IEnumerator LiftPlaceCoroutine(
            Vector3 fromWorld,
            Vector3 toWorld,
            bool snapToGrid,
            DiceState toState,
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

            positionRoot.SetParent(transform, true);
            positionRoot.localRotation = Quaternion.identity;
            positionRoot.localScale = Vector3.one;
            positionRoot.position = fromWorld;

            var elapsed = 0f;
            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / rollDuration);
                positionRoot.position = Vector3.Lerp(fromWorld, toWorld, t);
                yield return null;
            }

            positionRoot.position = toWorld;

            if (snapToGrid && board != null) {
                SnapTo(toState, board, registry);
            }

            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
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
                dissolveProgress = Mathf.Min(1f, dissolveProgress + Time.deltaTime / dissolveDuration);
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
                surfaceBaseWorldY - minY,
                gridWorldPosition.z);
            ApplyDissolveGhostVisual(progress);
        }

        void ApplyDissolveGhostVisual(float progress) {
            ApplyDissolveAlpha(progress);
            EnsurePushBody();
            pushBody?.SetCollisionEnabled(!IsDissolveGhost);
        }

        void ResetDissolveVisuals() {
            ApplyDissolveAlpha(0f);
            EnsurePushBody();
            pushBody?.SetCollisionEnabled(true);
        }

        void ApplyDissolveAlpha(float progress) {
            if (dissolveMaterials.Count == 0) {
                return;
            }

            var useTransparent = progress >= dissolveGhostThreshold;
            if (useTransparent != dissolveMaterialsTransparent) {
                dissolveMaterialsTransparent = useTransparent;
                for (var i = 0; i < dissolveMaterials.Count; i++) {
                    SetMaterialSurfaceType(dissolveMaterials[i], useTransparent);
                }
            }

            var alpha = useTransparent ? dissolveGhostAlpha : 1f;

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
