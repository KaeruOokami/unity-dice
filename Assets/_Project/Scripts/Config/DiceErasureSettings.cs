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

        [Header("Shared Emission")]
        [FormerlySerializedAs("dissolveEmissionColor")]
        [SerializeField] Color erasureEmissionColor = new Color(0.4f, 0.8f, 1f, 1f);
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
        public Color ErasureEmissionColor => erasureEmissionColor;
        public float ErasureEmissionIntensity => erasureEmissionIntensity;
        public Texture ErasureEmissionMap => erasureEmissionMap;
        public float ErasureEmissionPulseSpeed => erasureEmissionPulseSpeed;
        public float ErasureEmissionPulseMin => erasureEmissionPulseMin;
        public float ErasureEmissionPulseMax => erasureEmissionPulseMax;

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
