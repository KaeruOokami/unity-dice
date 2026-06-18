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
        [SerializeField] float rollDuration = 0.3f;

        Transform diceInstance;
        Coroutine rollCoroutine;
        bool isAnimating;

        public bool IsAnimating => isAnimating;

        public Transform DiceTransform {
            get {
                EnsureDiceInstance();
                return diceInstance;
            }
        }

        public void EnsureDiceInstance() {
            if (diceInstance != null) {
                return;
            }

            var prefab = ResolveMeshPrefab();
            if (prefab == null) {
                return;
            }

            var visual = UnityEngine.Object.Instantiate(prefab, transform);
            visual.name = "DiceVisual";
            diceInstance = visual.transform;
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

        public void SnapTo(DiceState state, Board board) {
            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
                rollCoroutine = null;
            }

            isAnimating = false;
            EnsureDiceInstance();
            if (diceInstance == null) {
                return;
            }

            diceInstance.SetParent(transform);
            diceInstance.position = board.GridToWorld(state.GridPos);
            diceInstance.rotation = DiceOrientationMapper.ToRotation(state.Orientation);
        }

        public void PlayRoll(Direction direction, DiceState fromState, DiceState toState, Board board, Action onComplete) {
            if (rollCoroutine != null) {
                StopCoroutine(rollCoroutine);
            }

            rollCoroutine = StartCoroutine(RollCoroutine(direction, fromState, toState, board, onComplete));
        }

        IEnumerator RollCoroutine(Direction direction, DiceState fromState, DiceState toState, Board board, Action onComplete) {
            isAnimating = true;
            EnsureDiceInstance();
            if (diceInstance == null) {
                isAnimating = false;
                onComplete?.Invoke();
                yield break;
            }

            SnapTo(fromState, board);

            var half = board.CellSize * 0.5f;
            var setup = DiceRollTransform.GetRollSetup(direction, half);
            var diceCenter = board.GridToWorld(fromState.GridPos);
            var targetRotation = DiceRollTransform.GetRollRotation(direction) *
                                 DiceOrientationMapper.ToRotation(fromState.Orientation);

            var pivotObject = new GameObject("RollPivot");
            var pivot = pivotObject.transform;
            pivot.position = diceCenter + setup.PivotOffset;
            pivot.rotation = Quaternion.identity;

            diceInstance.SetParent(pivot, true);

            var elapsed = 0f;
            while (elapsed < rollDuration) {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / rollDuration);
                pivot.rotation = Quaternion.AngleAxis(setup.Angle * t, setup.Axis);
                yield return null;
            }

            pivot.rotation = Quaternion.AngleAxis(setup.Angle, setup.Axis);

            diceInstance.SetParent(transform);
            Destroy(pivotObject);

            diceInstance.position = board.GridToWorld(toState.GridPos);
            diceInstance.rotation = targetRotation;

            isAnimating = false;
            rollCoroutine = null;
            onComplete?.Invoke();
        }
    }
}
