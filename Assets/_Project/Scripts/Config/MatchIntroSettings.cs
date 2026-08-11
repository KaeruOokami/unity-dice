using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "MatchIntroSettings", menuName = "Dice/Match Intro Settings")]
    public sealed class MatchIntroSettings : ScriptableObject
    {
        [SerializeField] string readyText = "Ready";
        [SerializeField] string startText = "Start";
        [SerializeField] float readyDurationSeconds = 2f;
        [SerializeField] float startDurationSeconds = 1f;
        [SerializeField] float centerFontSize = 120f;
        [SerializeField] Color textColor = Color.black;
        [SerializeField] Vector2 anchoredPosition;
        [SerializeField] Vector2 textAreaSize = new(800f, 200f);

        public string ReadyText => readyText;
        public string StartText => startText;
        public float ReadyDurationSeconds => readyDurationSeconds;
        public float StartDurationSeconds => startDurationSeconds;
        public float CenterFontSize => centerFontSize;
        public Color TextColor => textColor;
        public Vector2 AnchoredPosition => anchoredPosition;
        public Vector2 TextAreaSize => textAreaSize;

        void OnValidate() {
            readyDurationSeconds = Mathf.Max(0f, readyDurationSeconds);
            startDurationSeconds = Mathf.Max(0f, startDurationSeconds);
            centerFontSize = Mathf.Max(1f, centerFontSize);
            textAreaSize.x = Mathf.Max(1f, textAreaSize.x);
            textAreaSize.y = Mathf.Max(1f, textAreaSize.y);
            if (string.IsNullOrWhiteSpace(readyText)) {
                readyText = "Ready";
            }

            if (string.IsNullOrWhiteSpace(startText)) {
                startText = "Start";
            }
        }
    }
}
