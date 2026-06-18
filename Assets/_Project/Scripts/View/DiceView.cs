using System;
using System.Collections;
using DiceGame.Grid;
using DiceGame.Core;
using UnityEngine;

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

        Transform meshInstance;
        Coroutine rollCoroutine;
        Coroutine dissolveCoroutine;
        bool isAnimating;
        float dissolveProgress;
        int currentTopFace = 1;
        Vector3 gridWorldPosition;
        Board dissolveBoard;

        public bool IsAnimating => isAnimating;
        public float DissolveProgress => dissolveProgress;

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

            foreach (var collider in visual.GetComponentsInChildren<Collider>()) {
                collider.enabled = false;
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

        public void SnapTo(DiceState state, Board board) {
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
            ApplySurfaceVisual(board, 0f);
        }

        public void PlayRoll(Direction direction, DiceState fromState, DiceState toState, Board board, Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(RollCoroutine(direction, fromState, toState, board, onComplete));
        }

        public void PlaySlide(DiceState fromState, DiceState toState, Board board, Action onComplete) {
            if (dissolveCoroutine != null) {
                return;
            }

            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(SlideCoroutine(fromState, toState, board, onComplete));
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

        IEnumerator RollCoroutine(Direction direction, DiceState fromState, DiceState toState, Board board, Action onComplete) {
            isAnimating = true;
            EnsureMesh();
            if (positionRoot == null || rotationRoot == null) {
                isAnimating = false;
                onComplete?.Invoke();
                yield break;
            }

            SnapTo(fromState, board);

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

            currentTopFace = toState.Orientation.Top;
            gridWorldPosition = board.GridToWorld(toState.GridPos);
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(toState.Orientation);
            ApplySurfaceVisual(board, dissolveProgress);

            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }

        IEnumerator SlideCoroutine(DiceState fromState, DiceState toState, Board board, Action onComplete) {
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

            var fromWorld = board.GridToWorld(fromState.GridPos);
            var toWorld = board.GridToWorld(toState.GridPos);
            var elapsed = 0f;

            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / rollDuration);
                gridWorldPosition = Vector3.Lerp(fromWorld, toWorld, t);
                ApplySurfaceVisual(board, dissolveProgress);
                yield return null;
            }

            currentTopFace = toState.Orientation.Top;
            gridWorldPosition = toWorld;
            rotationRoot.rotation = DiceOrientationMapper.ToRotation(toState.Orientation);
            ApplySurfaceVisual(board, dissolveProgress);

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
                board.FloorSurfaceWorldY - minY,
                gridWorldPosition.z);
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
