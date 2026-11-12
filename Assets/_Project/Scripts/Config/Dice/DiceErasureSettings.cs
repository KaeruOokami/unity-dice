using UnityEngine;
using UnityEngine.Serialization;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceErasureSettings", menuName = "Dice/Dice/Erasure Settings")]
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

        public float SinkDuration => sinkDuration;
        public float SinkGhostThreshold => sinkGhostThreshold;
        public float SinkGhostAlpha => sinkGhostAlpha;
        public float RadianceDuration => radianceDuration;
        public float RadianceRampUpDuration => radianceRampUpDuration;

        void OnValidate() {
            sinkDuration = Mathf.Max(0.01f, sinkDuration);
            sinkGhostThreshold = Mathf.Clamp01(sinkGhostThreshold);
            sinkGhostAlpha = Mathf.Clamp01(sinkGhostAlpha);
            radianceDuration = Mathf.Max(0.01f, radianceDuration);
            radianceRampUpDuration = Mathf.Max(0f, radianceRampUpDuration);
        }
    }
}
