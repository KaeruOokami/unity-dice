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
        AttackQueueUiSettings uiSettings;
        DiceIconGenerator iconGenerator;
        RectTransform player1Panel;
        RectTransform player2Panel;

        public void Configure(
            DiceCatalog targetPlayer1Catalog,
            DiceCatalog targetPlayer2Catalog,
            AttackQueueUiSettings targetUiSettings) {
            player1Catalog = targetPlayer1Catalog;
            player2Catalog = targetPlayer2Catalog;
            uiSettings = targetUiSettings != null
                ? targetUiSettings
                : AttackQueueUiSettings.CreateRuntimeFallback();

            EnsureUi();
            iconGenerator?.Dispose();
            iconGenerator = new DiceIconGenerator(transform, uiSettings);
        }

        public void RenderAll(
            IReadOnlyList<AttackVolley> player1Volleys,
            IReadOnlyList<AttackVolley> player2Volleys) {
            if (iconGenerator == null) {
                return;
            }

            RenderForSlot(PlayerSlot.Player1, player1Volleys, player1Catalog, player1Panel);
            RenderForSlot(PlayerSlot.Player2, player2Volleys, player2Catalog, player2Panel);
        }

        void OnDestroy() {
            iconGenerator?.Dispose();
            iconGenerator = null;
        }

        void EnsureUi() {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) {
                var canvasObject = new GameObject("AttackQueueCanvas");
                canvasObject.transform.SetParent(transform, false);
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            player1Panel = EnsurePanel(
                canvas.transform,
                "Player1QueuePanel",
                uiSettings.Player1PanelLayout);
            player2Panel = EnsurePanel(
                canvas.transform,
                "Player2QueuePanel",
                uiSettings.Player2PanelLayout);
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

            var reverseColumns = defenderSlot == PlayerSlot.Player2;
            for (var columnIndex = 0; columnIndex < volleys.Count; columnIndex++) {
                var volleyIndex = reverseColumns ? volleys.Count - 1 - columnIndex : columnIndex;
                var volley = volleys[volleyIndex];
                if (volley == null || volley.Count == 0) {
                    continue;
                }

                var columnRoot = CreateColumn(panel);
                var iconCount = 0;
                for (var row = 0; row < volley.Count; row++) {
                    var spec = volley.Dice[row];
                    if (!catalog.TryGetMeshPrefab(spec.Kind, out var meshPrefab) || meshPrefab == null) {
                        continue;
                    }

                    if (!iconGenerator.TryGetSprite(meshPrefab, spec.Pip, out var sprite) || sprite == null) {
                        continue;
                    }

                    CreateIcon(columnRoot, sprite, defenderSlot, volleyIndex, row, spec);
                    iconCount++;
                }

                if (iconCount == 0) {
                    Destroy(columnRoot.gameObject);
                    continue;
                }

                ApplyColumnLayoutElement(columnRoot, iconCount);
            }
        }

        RectTransform EnsurePanel(
            Transform canvasTransform,
            string panelName,
            AttackQueuePanelLayout layout) {
            var existing = canvasTransform.Find(panelName) as RectTransform;
            if (existing != null) {
                ApplyPanelLayout(existing, layout);
                ApplyPanelLayoutGroup(existing, layout);
                return existing;
            }

            var panelObject = new GameObject(panelName, typeof(RectTransform));
            panelObject.transform.SetParent(canvasTransform, false);
            var panel = panelObject.GetComponent<RectTransform>();
            ApplyPanelLayout(panel, layout);

            var layoutGroup = panelObject.AddComponent<HorizontalLayoutGroup>();
            ApplyPanelLayoutGroup(panel, layout, layoutGroup);
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            return panel;
        }

        static void ApplyPanelLayout(RectTransform panel, AttackQueuePanelLayout layout) {
            panel.anchorMin = layout.AnchorMin;
            panel.anchorMax = layout.AnchorMax;
            panel.pivot = layout.Pivot;
            panel.anchoredPosition = Vector2.zero;
        }

        void ApplyPanelLayoutGroup(
            RectTransform panel,
            AttackQueuePanelLayout layout,
            HorizontalLayoutGroup layoutGroup = null) {
            layoutGroup ??= panel.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null) {
                return;
            }

            layoutGroup.spacing = uiSettings.ColumnSpacing;
            layoutGroup.childAlignment = ResolveChildAlignment(layout.Pivot);
        }

        static TextAnchor ResolveChildAlignment(Vector2 pivot) {
            if (pivot.y >= 0.5f) {
                return pivot.x >= 0.5f ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            }

            return pivot.x >= 0.5f ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
        }

        RectTransform CreateColumn(RectTransform panel) {
            var columnObject = new GameObject("Column", typeof(RectTransform));
            columnObject.transform.SetParent(panel, false);

            var column = columnObject.GetComponent<RectTransform>();
            columnObject.AddComponent<LayoutElement>();
            var layoutGroup = columnObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = uiSettings.RowSpacing;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            return column;
        }

        void ApplyColumnLayoutElement(RectTransform column, int iconCount) {
            var iconSize = uiSettings.IconSize;
            var height = iconSize * iconCount + uiSettings.RowSpacing * Mathf.Max(0, iconCount - 1);
            var layoutElement = column.GetComponent<LayoutElement>();
            if (layoutElement == null) {
                layoutElement = column.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minWidth = iconSize;
            layoutElement.preferredWidth = iconSize;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
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

            var iconSize = uiSettings.IconSize;
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
