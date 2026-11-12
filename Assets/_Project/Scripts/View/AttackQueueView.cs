using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Versus.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.View
{
    public class AttackQueueView : MonoBehaviour
    {
        DiceCatalog player1Catalog;
        DiceCatalog player2Catalog;
        AttackQueueSettings settings;
        DiceIconGenerator iconGenerator;
        Canvas canvas;
        RectTransform canvasRect;
        RectTransform player1Panel;
        RectTransform player2Panel;
        Coroutine prewarmRoutine;
        int prewarmGeneration;

        public bool AreIconsReady { get; private set; }

        /// <summary>
        /// Panel-local center of the queue home slot (oldest / next-to-board column).
        /// P1: left edge. P2: right edge. Independent of whether icons currently exist.
        /// </summary>
        public bool TryGetQueueHomePanelLocal(PlayerSlot defenderSlot, out Vector2 panelLocal) {
            panelLocal = default;
            if (settings == null) {
                Debug.LogError("AttackQueueView: Cannot resolve queue home because settings are null.");
                return false;
            }

            if (GetPanel(defenderSlot) == null) {
                Debug.LogError(
                    $"AttackQueueView: Cannot resolve queue home because panel is missing for {defenderSlot}.");
                return false;
            }

            panelLocal = ResolveQueueHomePanelLocal(defenderSlot);
            return true;
        }

        /// <summary>
        /// Panel-local center of the column whose oldest-first index is <paramref name="oldestFirstIndex"/>
        /// (0 = home / next-to-board).
        /// </summary>
        public bool TryGetQueueColumnPanelLocal(
            PlayerSlot defenderSlot,
            int oldestFirstIndex,
            out Vector2 panelLocal) {
            panelLocal = default;
            if (!TryGetQueueHomePanelLocal(defenderSlot, out var home)) {
                return false;
            }

            var index = Mathf.Max(0, oldestFirstIndex);
            var step = settings.IconSize + settings.ColumnSpacing;
            var growRight = defenderSlot != PlayerSlot.Player2;
            panelLocal = home + new Vector2(growRight ? index * step : -index * step, 0f);
            return true;
        }

        /// <summary>
        /// Projects match-origin dice and queue home onto one camera-depth plane for attack-send VFX.
        /// </summary>
        public bool TryResolveAttackSendWorldPoints(
            PlayerSlot defenderSlot,
            Vector3 matchOriginWorldPosition,
            Camera worldCamera,
            out Vector3 startWorldOnCameraPlane,
            out Vector3 endWorldOnCameraPlane) {
            startWorldOnCameraPlane = default;
            endWorldOnCameraPlane = default;

            if (worldCamera == null) {
                Debug.LogError("AttackQueueView: Attack send VFX needs a world camera (Camera.main is null).");
                return false;
            }

            var panel = GetPanel(defenderSlot);
            if (panel == null) {
                Debug.LogError(
                    $"AttackQueueView: Attack send VFX failed because queue panel is missing for {defenderSlot}.");
                return false;
            }

            if (!TryGetQueueHomePanelLocal(defenderSlot, out var homeLocal)) {
                return false;
            }

            var startScreen = worldCamera.WorldToScreenPoint(matchOriginWorldPosition);
            if (startScreen.z < 0f) {
                Debug.LogError(
                    "AttackQueueView: Match origin is behind the camera; cannot project attack-send VFX start.");
                return false;
            }

            var endUiWorld = panel.TransformPoint(new Vector3(homeLocal.x, homeLocal.y, 0f));
            var endScreen = RectTransformUtility.WorldToScreenPoint(
                ResolveCanvasEventCamera(),
                endUiWorld);

            var planeDepth = startScreen.z;
            startWorldOnCameraPlane = worldCamera.ScreenToWorldPoint(
                new Vector3(startScreen.x, startScreen.y, planeDepth));
            endWorldOnCameraPlane = worldCamera.ScreenToWorldPoint(
                new Vector3(endScreen.x, endScreen.y, planeDepth));
            return true;
        }

        Vector2 ResolveQueueHomePanelLocal(PlayerSlot defenderSlot) {
            var iconHalf = settings.IconSize * 0.5f;
            return defenderSlot == PlayerSlot.Player2
                ? new Vector2(-iconHalf, -iconHalf)
                : new Vector2(iconHalf, -iconHalf);
        }

        RectTransform GetPanel(PlayerSlot defenderSlot) {
            return defenderSlot == PlayerSlot.Player1 ? player1Panel : player2Panel;
        }

        Camera ResolveCanvasEventCamera() {
            if (canvas == null) {
                return null;
            }

            return canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        public void Configure(
            DiceCatalog targetPlayer1Catalog,
            DiceCatalog targetPlayer2Catalog,
            AttackQueueSettings targetSettings) {
            player1Catalog = targetPlayer1Catalog;
            player2Catalog = targetPlayer2Catalog;
            settings = targetSettings != null
                ? targetSettings
                : AttackQueueSettings.CreateRuntimeFallback();

            EnsureUi();
            iconGenerator?.Dispose();
            iconGenerator = new DiceIconGenerator(transform, settings);
            AreIconsReady = false;
            prewarmGeneration++;
            if (prewarmRoutine != null) {
                StopCoroutine(prewarmRoutine);
            }

            prewarmRoutine = StartCoroutine(PrewarmIconsRoutine(prewarmGeneration));
        }

        public void RenderAll(
            IReadOnlyList<AttackVolley> player1Volleys,
            IReadOnlyList<AttackVolley> player2Volleys) {
            if (iconGenerator == null || !AreIconsReady) {
                return;
            }

            RenderForSlot(PlayerSlot.Player1, player1Volleys, player1Catalog, player1Panel);
            RenderForSlot(PlayerSlot.Player2, player2Volleys, player2Catalog, player2Panel);
        }

        /// <summary>
        /// Scales the home-column icons for the next-to-board volley from remaining delay.
        /// Call every pacing tick so rebuilds stay aligned with spawn.
        /// </summary>
        public void ApplyHeadDropAnticipation(PlayerSlot defenderSlot, float remainingDelaySeconds) {
            if (settings == null) {
                return;
            }

            var panel = GetPanel(defenderSlot);
            if (panel == null || panel.childCount == 0) {
                return;
            }

            var scale = AttackQueueDropAnticipation.EvaluateScale(remainingDelaySeconds, settings);
            ApplyColumnIconScale(panel.GetChild(0), scale);
        }

        public void ClearHeadDropAnticipation(PlayerSlot defenderSlot) {
            var panel = GetPanel(defenderSlot);
            if (panel == null || panel.childCount == 0) {
                return;
            }

            ApplyColumnIconScale(panel.GetChild(0), 1f);
        }

        static void ApplyColumnIconScale(Transform column, float scale) {
            if (column == null) {
                return;
            }

            var scaleVector = Vector3.one * scale;
            for (var i = 0; i < column.childCount; i++) {
                column.GetChild(i).localScale = scaleVector;
            }
        }

        void OnDestroy() {
            if (prewarmRoutine != null) {
                StopCoroutine(prewarmRoutine);
                prewarmRoutine = null;
            }

            iconGenerator?.Dispose();
            iconGenerator = null;
        }

        IEnumerator PrewarmIconsRoutine(int generation) {
            var meshes = CollectUniqueMeshPrefabs(player1Catalog, player2Catalog);
            var perFrame = settings.IconPrewarmPerFrame;
            var warmedThisFrame = 0;

            for (var meshIndex = 0; meshIndex < meshes.Count; meshIndex++) {
                var meshPrefab = meshes[meshIndex];
                for (var pip = DiceIconGenerator.MinPip; pip <= DiceIconGenerator.MaxPip; pip++) {
                    if (generation != prewarmGeneration || iconGenerator == null) {
                        yield break;
                    }

                    if (!iconGenerator.WarmSprite(meshPrefab, pip)) {
                        Debug.LogError(
                            $"AttackQueueView: Failed to warm icon mesh='{meshPrefab.name}' pip={pip}.");
                    }

                    warmedThisFrame++;
                    if (warmedThisFrame < perFrame) {
                        continue;
                    }

                    warmedThisFrame = 0;
                    yield return null;
                }
            }

            if (generation != prewarmGeneration) {
                yield break;
            }

            AreIconsReady = true;
            prewarmRoutine = null;
        }

        static List<GameObject> CollectUniqueMeshPrefabs(DiceCatalog catalogA, DiceCatalog catalogB) {
            var meshes = new List<GameObject>();
            var seenIds = new HashSet<int>();
            AppendCatalogMeshes(catalogA, meshes, seenIds);
            AppendCatalogMeshes(catalogB, meshes, seenIds);
            return meshes;
        }

        static void AppendCatalogMeshes(
            DiceCatalog catalog,
            List<GameObject> meshes,
            HashSet<int> seenIds) {
            if (catalog?.Entries == null) {
                return;
            }

            for (var i = 0; i < catalog.Entries.Length; i++) {
                var meshPrefab = catalog.Entries[i].MeshPrefab;
                if (meshPrefab == null) {
                    continue;
                }

                var id = meshPrefab.GetInstanceID();
                if (!seenIds.Add(id)) {
                    continue;
                }

                meshes.Add(meshPrefab);
            }
        }

        void EnsureUi() {
            canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) {
                var canvasObject = new GameObject("AttackQueueCanvas");
                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.AddComponent<Canvas>();

                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.sortingOrder = 100;

            canvasRect = canvas.transform as RectTransform;
            player1Panel = EnsurePanel(
                canvas.transform,
                "Player1QueuePanel",
                settings.Player1PanelLayout);
            player2Panel = EnsurePanel(
                canvas.transform,
                "Player2QueuePanel",
                settings.Player2PanelLayout);
        }

        void RenderForSlot(
            PlayerSlot defenderSlot,
            IReadOnlyList<AttackVolley> volleys,
            DiceCatalog catalog,
            RectTransform panel) {
            ClearPanel(panel);
            if (catalog == null || panel == null || volleys == null || volleys.Count == 0) {
                return;
            }

            // oldestFirstIndex 0 = home (next to board); higher = newer columns away from home.
            for (var oldestFirstIndex = 0; oldestFirstIndex < volleys.Count; oldestFirstIndex++) {
                var volley = volleys[oldestFirstIndex];
                if (volley == null || volley.Count == 0) {
                    continue;
                }

                if (!TryGetQueueColumnPanelLocal(defenderSlot, oldestFirstIndex, out var columnLocal)) {
                    continue;
                }

                var columnRoot = CreateColumn(panel, defenderSlot, columnLocal);
                var iconCount = 0;
                for (var row = 0; row < volley.Count; row++) {
                    var spec = volley.Dice[row];
                    if (!catalog.TryGetMeshPrefab(spec.Kind, out var meshPrefab) || meshPrefab == null) {
                        Debug.LogWarning(
                            $"[QueueView] defender={defenderSlot} volley={oldestFirstIndex} row={row} " +
                            $"meshMissing kind={spec.Kind}");
                        continue;
                    }

                    if (!iconGenerator.TryGetCachedSprite(meshPrefab, spec.Pip, out var sprite)
                        || sprite == null) {
                        Debug.LogError(
                            $"[QueueView] defender={defenderSlot} volley={oldestFirstIndex} row={row} " +
                            $"cacheMiss kind={spec.Kind} pip={spec.Pip} mesh='{meshPrefab.name}'.");
                        continue;
                    }

                    CreateIcon(columnRoot, sprite, defenderSlot, oldestFirstIndex, row, spec);
                    iconCount++;
                }

                if (iconCount == 0) {
                    Destroy(columnRoot.gameObject);
                    continue;
                }

                ApplyColumnSize(columnRoot, iconCount);
            }
        }

        RectTransform EnsurePanel(
            Transform canvasTransform,
            string panelName,
            AttackQueuePanelLayout layout) {
            var existing = canvasTransform.Find(panelName) as RectTransform;
            if (existing != null) {
                ApplyPanelLayout(existing, layout);
                RemoveHorizontalLayoutGroup(existing);
                return existing;
            }

            var panelObject = new GameObject(panelName, typeof(RectTransform));
            panelObject.transform.SetParent(canvasTransform, false);
            var panel = panelObject.GetComponent<RectTransform>();
            ApplyPanelLayout(panel, layout);
            return panel;
        }

        static void ApplyPanelLayout(RectTransform panel, AttackQueuePanelLayout layout) {
            panel.anchorMin = layout.AnchorMin;
            panel.anchorMax = layout.AnchorMax;
            panel.pivot = layout.Pivot;
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = Vector2.zero;
        }

        static void RemoveHorizontalLayoutGroup(RectTransform panel) {
            var layoutGroup = panel.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup != null) {
                Destroy(layoutGroup);
            }
        }

        RectTransform CreateColumn(
            RectTransform panel,
            PlayerSlot defenderSlot,
            Vector2 panelLocal) {
            var columnObject = new GameObject("Column", typeof(RectTransform));
            columnObject.transform.SetParent(panel, false);

            var column = columnObject.GetComponent<RectTransform>();
            var panelPivot = defenderSlot == PlayerSlot.Player2 ? Vector2.one : new Vector2(0f, 1f);
            column.anchorMin = panelPivot;
            column.anchorMax = panelPivot;
            column.pivot = new Vector2(0.5f, 1f);
            column.anchoredPosition = panelLocal;

            var layoutGroup = columnObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = settings.RowSpacing;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            return column;
        }

        void ApplyColumnSize(RectTransform column, int iconCount) {
            var iconSize = settings.IconSize;
            var height = iconSize * iconCount + settings.RowSpacing * Mathf.Max(0, iconCount - 1);
            column.sizeDelta = new Vector2(iconSize, height);
        }

        void CreateIcon(
            RectTransform columnRoot,
            Sprite sprite,
            PlayerSlot defenderSlot,
            int column,
            int row,
            AttackDieSpec spec) {
            var iconObject = new GameObject(
                $"AttackIcon_{defenderSlot}_{column}_{row}_{spec.Kind}_{spec.Pip}",
                typeof(RectTransform));
            iconObject.transform.SetParent(columnRoot, false);

            var image = iconObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = settings.FireIconIdleColor;

            if (spec.IsOnFire) {
                var pulse = iconObject.AddComponent<AttackQueueFireIconPulse>();
                pulse.Configure(settings);
            }

            var iconSize = settings.IconSize;
            var layoutElement = iconObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = iconSize;
            layoutElement.preferredHeight = iconSize;
            layoutElement.minWidth = iconSize;
            layoutElement.minHeight = iconSize;

            var rectTransform = iconObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        }

        static void ClearPanel(RectTransform panel) {
            if (panel == null) {
                return;
            }

            for (var i = panel.childCount - 1; i >= 0; i--) {
                Destroy(panel.GetChild(i).gameObject);
            }
        }
    }
}
