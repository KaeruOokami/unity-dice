using DiceGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Gameplay
{
    public sealed class MatchIntroUi : MonoBehaviour
    {
        TMP_FontAsset uiFont;
        MatchIntroSettings introSettings;
        Canvas canvas;
        RectTransform textRect;
        TextMeshProUGUI centerText;

        public void Configure(UiFontSettings fontSettings, MatchIntroSettings settings) {
            if (fontSettings == null || !fontSettings.TryGetPrimaryFont(out uiFont)) {
                Debug.LogError("[MatchIntroUi] UiFontSettings primary font is not assigned.");
                return;
            }

            if (settings == null) {
                Debug.LogError("[MatchIntroUi] MatchIntroSettings is not assigned.");
                return;
            }

            introSettings = settings;
            BuildUi();
            ApplyLayout();
            Hide();
        }

        public void Show(string message) {
            if (canvas == null || centerText == null || introSettings == null) {
                return;
            }

            ApplyLayout();
            centerText.text = message ?? string.Empty;
            canvas.gameObject.SetActive(true);
        }

        public void Hide() {
            if (canvas != null) {
                canvas.gameObject.SetActive(false);
            }
        }

        void ApplyLayout() {
            if (introSettings == null || centerText == null) {
                return;
            }

            centerText.fontSize = introSettings.CenterFontSize;
            centerText.color = introSettings.TextColor;

            if (textRect != null) {
                textRect.anchoredPosition = introSettings.AnchoredPosition;
                textRect.sizeDelta = introSettings.TextAreaSize;
            }
        }

        void BuildUi() {
            if (canvas != null) {
                return;
            }

            var canvasObject = new GameObject("MatchIntroCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var textObject = new GameObject("CenterText");
            textObject.transform.SetParent(canvasObject.transform, false);
            textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(800f, 200f);
            textRect.anchoredPosition = Vector2.zero;

            centerText = textObject.AddComponent<TextMeshProUGUI>();
            centerText.font = uiFont;
            centerText.alignment = TextAlignmentOptions.Center;
            centerText.textWrappingMode = TextWrappingModes.NoWrap;
            centerText.overflowMode = TextOverflowModes.Overflow;
            centerText.raycastTarget = false;
        }
    }
}
