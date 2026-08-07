using System;
using DiceGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceGame.Gameplay
{
    public enum PauseMenuRole
    {
        /// <summary>Resume + return to title (host / local).</summary>
        Host,
        /// <summary>Resume only (online client).</summary>
        Client
    }

    public sealed class PauseMenuUi : MonoBehaviour
    {
        UiFontSettings uiFontSettings;

        Canvas canvas;
        GameObject panel;
        Button resumeButton;
        Button titleButton;
        TextMeshProUGUI subtitleText;
        PauseMenuRole currentRole = PauseMenuRole.Host;
        bool canOperatePause = true;

        public event Action ResumeClicked;
        public event Action ReturnToTitleClicked;

        public void Configure(UiFontSettings fontSettings) {
            if (fontSettings == null || !fontSettings.TryGetPrimaryFont(out _)) {
                Debug.LogError("[PauseMenuUi] UiFontSettings is not assigned or primary font is missing.");
                return;
            }

            uiFontSettings = fontSettings;
            EnsureEventSystem();
            BuildUi();
            Hide();
        }

        public void Show(PauseMenuRole role, bool canOperate) {
            currentRole = role;
            canOperatePause = canOperate;
            if (canvas != null) {
                canvas.gameObject.SetActive(true);
            }

            if (panel != null) {
                panel.SetActive(true);
            }

            if (resumeButton != null) {
                resumeButton.interactable = canOperate;
            }

            if (titleButton != null) {
                // Host/Client UI difference (Title only on Host) stays as-is;
                // operability is gated by who initiated the pause.
                titleButton.gameObject.SetActive(role == PauseMenuRole.Host);
                titleButton.interactable = role == PauseMenuRole.Host && canOperate;
            }

            if (subtitleText != null) {
                subtitleText.text = canOperate
                    ? string.Empty
                    : "Paused by opponent (controls locked)";
            }
        }

        public void Hide() {
            if (canvas != null) {
                canvas.gameObject.SetActive(false);
            }
        }

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;

        void BuildUi() {
            if (canvas != null) {
                return;
            }

            var canvasObject = new GameObject("PauseMenuCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var dim = CreatePanel(canvasObject.transform, "Dim", new Color(0f, 0f, 0f, 0.65f));
            StretchFull(dim.GetComponent<RectTransform>());

            panel = CreatePanel(dim.transform, "Panel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            CenterPanel(panel.GetComponent<RectTransform>(), new Vector2(480f, 340f));

            CreateText(panel.transform, "Title", "Pause", 40, TextAnchor.UpperCenter);
            subtitleText = CreateText(panel.transform, "Subtitle", string.Empty, 22, TextAnchor.UpperCenter);
            var subtitleRect = subtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.1f, 0.55f);
            subtitleRect.anchorMax = new Vector2(0.9f, 0.7f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            resumeButton = CreateButton(panel.transform, "ResumeButton", "Resume", new Vector2(0f, 10f), () => {
                if (canOperatePause) {
                    ResumeClicked?.Invoke();
                }
            });
            titleButton = CreateButton(panel.transform, "TitleButton", "Return to Title", new Vector2(0f, -80f), () => {
                if (currentRole == PauseMenuRole.Host && canOperatePause) {
                    ReturnToTitleClicked?.Invoke();
                }
            });
        }

        static void EnsureEventSystem() {
            if (FindFirstObjectByType<EventSystem>() != null) {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        static GameObject CreatePanel(Transform parent, string name, Color color) {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            // Image already adds RectTransform; do not AddComponent again.
            return panelObject;
        }

        TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor anchor) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            StretchFull(rect);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = uiFontSettings.PrimaryFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = ToTextAlignment(anchor);
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Action onClick) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 56f);
            rect.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.45f, 0.85f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            CreateText(go.transform, "Label", label, 26, TextAnchor.MiddleCenter);
            return button;
        }

        static void StretchFull(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void CenterPanel(RectTransform rect, Vector2 size) {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        static TextAlignmentOptions ToTextAlignment(TextAnchor anchor) {
            return anchor switch {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.MidlineLeft
            };
        }
    }
}
