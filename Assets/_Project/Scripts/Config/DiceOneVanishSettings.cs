using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceOneVanishSettings", menuName = "Dice/Dice One Vanish Settings")]
    public class DiceOneVanishSettings : ScriptableObject
    {
        [SerializeField] float emissionIntensity = 3f;
        [SerializeField] float rampUpDuration = 0.25f;
        [SerializeField] float glowDuration = 0.5f;
        [SerializeField] float vanishDuration = 1f;

        public float EmissionIntensity => emissionIntensity;
        public float RampUpDuration => rampUpDuration;
        public float GlowDuration => glowDuration;
        public float VanishDuration => vanishDuration;

        void OnValidate() {
            emissionIntensity = Mathf.Max(0f, emissionIntensity);
            rampUpDuration = Mathf.Max(0f, rampUpDuration);
            glowDuration = Mathf.Max(0f, glowDuration);
            vanishDuration = Mathf.Max(0.01f, vanishDuration);
        }
    }
}
