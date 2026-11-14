using UnityEngine;

namespace DiceGame.Config
{
    /// <summary>
    /// Attack presentation only (erasure emission tint). Attack logic lives in
    /// <see cref="PlayerAttackSettings"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerAttackPresentationSettings",
        menuName = "Dice/Match Setup/Player Attack Presentation")]
    public sealed class PlayerAttackPresentationSettings : ScriptableObject
    {
        [SerializeField] Color erasureEmissionColor = Color.white;

        public Color ErasureEmissionColor =>
            erasureEmissionColor.a <= 0f
                ? Color.white
                : WithOpaqueAlpha(erasureEmissionColor);

        public static PlayerAttackPresentationSettings CreateRuntime(Color color) {
            var instance = CreateInstance<PlayerAttackPresentationSettings>();
            instance.Apply(color);
            return instance;
        }

        public void Apply(Color color) {
            erasureEmissionColor = WithOpaqueAlpha(color);
        }

        static Color WithOpaqueAlpha(Color color) {
            return new Color(color.r, color.g, color.b, 1f);
        }
    }
}
