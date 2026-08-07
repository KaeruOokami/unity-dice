using TMPro;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "UiFontSettings", menuName = "Dice/UI Font Settings")]
    public sealed class UiFontSettings : ScriptableObject
    {
        [SerializeField] TMP_FontAsset primaryFont;

        public TMP_FontAsset PrimaryFont => primaryFont;

        public bool TryGetPrimaryFont(out TMP_FontAsset font) {
            font = primaryFont;
            if (font != null) {
                return true;
            }

            Debug.LogError($"[UiFontSettings] Primary font is not assigned on '{name}'.");
            return false;
        }
    }
}
