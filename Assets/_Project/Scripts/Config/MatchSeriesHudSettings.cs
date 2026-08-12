using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "MatchSeriesHudSettings", menuName = "Dice/Match Series Hud Settings")]
    public sealed class MatchSeriesHudSettings : ScriptableObject
    {
        [SerializeField] float fontSize = 36f;
        [SerializeField] Color textColor = Color.black;
        [SerializeField] Vector2 player1AnchoredPosition = new(40f, -40f);
        [SerializeField] Vector2 player2AnchoredPosition = new(-40f, -40f);
        [SerializeField] Vector2 textAreaSize = new(420f, 64f);

        public float FontSize => fontSize;
        public Color TextColor => textColor;
        public Vector2 Player1AnchoredPosition => player1AnchoredPosition;
        public Vector2 Player2AnchoredPosition => player2AnchoredPosition;
        public Vector2 TextAreaSize => textAreaSize;

        void OnValidate() {
            fontSize = Mathf.Max(1f, fontSize);
            textAreaSize.x = Mathf.Max(1f, textAreaSize.x);
            textAreaSize.y = Mathf.Max(1f, textAreaSize.y);
        }
    }
}
