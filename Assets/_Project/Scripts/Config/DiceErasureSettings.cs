using UnityEngine;
using UnityEngine.Serialization;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceErasureSettings", menuName = "Dice/Dice Erasure Settings")]
    public class DiceErasureSettings : ScriptableObject
    {
        [Header("Sink Erasure (Bottom)")]
        [FormerlySerializedAs("dissolveDuration")]
        [SerializeField] float sinkDuration = 0.8f;
        [FormerlySerializedAs("dissolveGhostThreshold")]
        [SerializeField] float sinkGhostThreshold = 0.45f;
        [FormerlySerializedAs("dissolveGhostAlpha")]
        [SerializeField] float sinkGhostAlpha = 0.35f;

        [Header("Radiance Erasure (Top)")]
        [SerializeField] float radianceDuration = 0.4f;
        [FormerlySerializedAs("radianceRampUpDuration")]
        [SerializeField] float radianceRampUpDuration = 0.1f;

        [Header("Neutral Emission")]
        [SerializeField] Color neutralEmissionColor = new(1f, 1f, 1f, 1f);

        [Header("Player 1 Emission")]
        [FormerlySerializedAs("erasureEmissionColor")]
        [SerializeField] Color player1EmissionColor = new(0.2f, 0.6f, 1f, 1f);

        [Header("Player 2 Emission")]
        [SerializeField] Color player2EmissionColor = new(1f, 0.35f, 0.2f, 1f);

        [Header("Shared Emission")]
        [FormerlySerializedAs("dissolveEmissionIntensity")]
        [SerializeField] float erasureEmissionIntensity = 2f;
        [FormerlySerializedAs("dissolveEmissionMap")]
        [SerializeField] Texture erasureEmissionMap;
        [FormerlySerializedAs("dissolveEmissionPulseSpeed")]
        [SerializeField] float erasureEmissionPulseSpeed = 4f;
        [FormerlySerializedAs("dissolveEmissionPulseMin")]
        [SerializeField] float erasureEmissionPulseMin = 0.55f;
        [FormerlySerializedAs("dissolveEmissionPulseMax")]
        [SerializeField] float erasureEmissionPulseMax = 1f;

        public float SinkDuration => sinkDuration;
        public float SinkGhostThreshold => sinkGhostThreshold;
        public float SinkGhostAlpha => sinkGhostAlpha;
        public float RadianceDuration => radianceDuration;
        public float RadianceRampUpDuration => radianceRampUpDuration;
        public float ErasureEmissionIntensity => erasureEmissionIntensity;
        public Texture ErasureEmissionMap => erasureEmissionMap;
        public float ErasureEmissionPulseSpeed => erasureEmissionPulseSpeed;
        public float ErasureEmissionPulseMin => erasureEmissionPulseMin;
        public float ErasureEmissionPulseMax => erasureEmissionPulseMax;

        public Color NeutralEmissionColor => neutralEmissionColor;

        public Color GetPlayerEmissionColor(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1EmissionColor : player2EmissionColor;
        }

        void OnValidate() {
            sinkDuration = Mathf.Max(0.01f, sinkDuration);
            sinkGhostThreshold = Mathf.Clamp01(sinkGhostThreshold);
            sinkGhostAlpha = Mathf.Clamp01(sinkGhostAlpha);
            radianceDuration = Mathf.Max(0.01f, radianceDuration);
            radianceRampUpDuration = Mathf.Max(0f, radianceRampUpDuration);
            erasureEmissionIntensity = Mathf.Max(0f, erasureEmissionIntensity);
            erasureEmissionPulseMin = Mathf.Max(0f, erasureEmissionPulseMin);
            erasureEmissionPulseMax = Mathf.Max(erasureEmissionPulseMin, erasureEmissionPulseMax);
        }
    }
}
