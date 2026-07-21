using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceGame.Gameplay
{
    public sealed class PauseMenuUi : MonoBehaviour
    {
        Canvas canvas;
        GameObject panel;
        Button resumeButton;
        Button titleButton;
        Text subtitleText;
        bool hostControlsEnabled = true;

        public event Action ResumeClicked;
        public event Action ReturnToTitleClicked;

        public void Configure() {
            EnsureEventSystem();
            BuildUi();
            Hide();
        }

        public void Show(bool allowHostActions) {
            hostControlsEnabled = allowHostActions;
            if (canvas != null) {
                canvas.gameObject.SetActive(true);
            }

            if (panel != null) {
                panel.SetActive(true);
            }

            if (resumeButton != null) {
                resumeButton.interactable = allowHostActions;
            }

            if (titleButton != null) {
                titleButton.interactable = allowHostActions;
            }

            if (subtitleText != null) {
                subtitleText.text = allowHostActions
                    ? string.Empty
                    : "Paused by host";
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
                if (hostControlsEnabled) {
                    ResumeClicked?.Invoke();
                }
            });
            titleButton = CreateButton(panel.transform, "TitleButton", "Return to Title", new Vector2(0f, -80f), () => {
                if (hostControlsEnabled) {
                    ReturnToTitleClicked?.Invoke();
                }
            });
        }

        static void EnsureEventSystem() {
            if (FindObjectOfType<EventSystem>() != null) {
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
            panelObject.AddComponent<RectTransform>();
            return panelObject;
        }

        static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor anchor) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            StretchFull(rect);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button CreateButton(
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
    }
}
