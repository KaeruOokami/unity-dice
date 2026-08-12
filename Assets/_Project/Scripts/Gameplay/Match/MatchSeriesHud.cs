using DiceGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Versus series score HUD: "1P 1/2" and "2P 0/2" (wins / wins-to-win).
    /// </summary>
    public sealed class MatchSeriesHud : MonoBehaviour
    {
        TMP_FontAsset uiFont;
        MatchSeriesHudSettings hudSettings;
        Canvas canvas;
        RectTransform player1Rect;
        RectTransform player2Rect;
        TextMeshProUGUI player1Text;
        TextMeshProUGUI player2Text;

        public void Configure(UiFontSettings fontSettings, MatchSeriesHudSettings settings) {
            if (fontSettings == null || !fontSettings.TryGetPrimaryFont(out uiFont)) {
                Debug.LogError("[MatchSeriesHud] UiFontSettings primary font is not assigned.");
                return;
            }

            if (settings == null) {
                Debug.LogError("[MatchSeriesHud] MatchSeriesHudSettings is not assigned.");
                return;
            }

            hudSettings = settings;
            BuildUi();
            ApplyLayout();
            MatchSeriesState.Changed -= Refresh;
            MatchSeriesState.Changed += Refresh;
            ChallengeRunState.Changed -= Refresh;
            ChallengeRunState.Changed += Refresh;
            Refresh();
        }

        void OnDestroy() {
            MatchSeriesState.Changed -= Refresh;
            ChallengeRunState.Changed -= Refresh;
        }

        public void Refresh() {
            if (player1Text == null || player2Text == null) {
                return;
            }

            if (ChallengeRunState.IsActive) {
                if (canvas != null) {
                    canvas.gameObject.SetActive(true);
                }

                ApplyLayout();
                player1Text.text =
                    $"Match  {ChallengeRunState.DisplayMatchNumber}/{ChallengeRunState.MatchCount}";
                player2Text.text = string.Empty;
                return;
            }

            if (!MatchSeriesState.IsActive) {
                if (canvas != null) {
                    canvas.gameObject.SetActive(false);
                }

                return;
            }

            if (canvas != null) {
                canvas.gameObject.SetActive(true);
            }

            ApplyLayout();
            var needed = MatchSeriesState.WinsToWin;
            player1Text.text = $"1P  {MatchSeriesState.Player1Wins}/{needed}";
            player2Text.text = $"2P  {MatchSeriesState.Player2Wins}/{needed}";
        }

        void ApplyLayout() {
            if (hudSettings == null) {
                return;
            }

            if (player1Text != null) {
                player1Text.fontSize = hudSettings.FontSize;
                player1Text.color = hudSettings.TextColor;
            }

            if (player2Text != null) {
                player2Text.fontSize = hudSettings.FontSize;
                player2Text.color = hudSettings.TextColor;
            }

            if (player1Rect != null) {
                player1Rect.anchoredPosition = hudSettings.Player1AnchoredPosition;
                player1Rect.sizeDelta = hudSettings.TextAreaSize;
            }

            if (player2Rect != null) {
                player2Rect.anchoredPosition = hudSettings.Player2AnchoredPosition;
                player2Rect.sizeDelta = hudSettings.TextAreaSize;
            }
        }

        void BuildUi() {
            if (canvas != null) {
                return;
            }

            var canvasObject = new GameObject("MatchSeriesHudCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            player1Text = CreateScoreText(canvasObject.transform, "Player1Score", TextAlignmentOptions.Left);
            player1Rect = player1Text.rectTransform;
            player1Rect.anchorMin = new Vector2(0f, 1f);
            player1Rect.anchorMax = new Vector2(0f, 1f);
            player1Rect.pivot = new Vector2(0f, 1f);

            player2Text = CreateScoreText(canvasObject.transform, "Player2Score", TextAlignmentOptions.Right);
            player2Rect = player2Text.rectTransform;
            player2Rect.anchorMin = new Vector2(1f, 1f);
            player2Rect.anchorMax = new Vector2(1f, 1f);
            player2Rect.pivot = new Vector2(1f, 1f);
        }

        TextMeshProUGUI CreateScoreText(Transform parent, string name, TextAlignmentOptions alignment) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = uiFont;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
