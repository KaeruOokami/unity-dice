using UnityEngine;
using UnityEngine.Serialization;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceEmissionPaletteSettings", menuName = "Dice/Dice/Emission Palette Settings")]
    public sealed class DiceEmissionPaletteSettings : ScriptableObject
    {
        [Header("Neutral Emission")]
        [SerializeField] Color neutralEmissionColor = new(1f, 1f, 1f, 1f);

        [Header("Shared Emission")]
        [FormerlySerializedAs("dissolveEmissionIntensity")]
        [SerializeField] float erasureEmissionIntensity = 2f;
        [FormerlySerializedAs("dissolveEmissionMap")]
        [SerializeField] Texture erasureEmissionMap;
        [FormerlySerializedAs("dissolveEmissionPulseSpeed")]
        [Tooltip("Shared blink angular speed (rad/sec via Time.time). Progress 1 forces PulseMax.")]
        [SerializeField] float erasureEmissionPulseSpeed = 4f;
        [FormerlySerializedAs("dissolveEmissionPulseMin")]
        [SerializeField] float erasureEmissionPulseMin = 0.55f;
        [FormerlySerializedAs("dissolveEmissionPulseMax")]
        [SerializeField] float erasureEmissionPulseMax = 1f;

        public float ErasureEmissionIntensity => erasureEmissionIntensity;
        public Texture ErasureEmissionMap => erasureEmissionMap;
        public float ErasureEmissionPulseSpeed => erasureEmissionPulseSpeed;
        public float ErasureEmissionPulseMin => erasureEmissionPulseMin;
        public float ErasureEmissionPulseMax => erasureEmissionPulseMax;

        public Color NeutralEmissionColor => neutralEmissionColor;

        void OnValidate() {
            erasureEmissionIntensity = Mathf.Max(0f, erasureEmissionIntensity);
            erasureEmissionPulseMin = Mathf.Max(0f, erasureEmissionPulseMin);
            erasureEmissionPulseMax = Mathf.Max(erasureEmissionPulseMin, erasureEmissionPulseMax);
        }
    }
}
