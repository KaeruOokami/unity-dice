using UnityEngine;
using UnityEngine.Serialization;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceErasureSettings", menuName = "Dice/Dice Erasure Settings")]
    public class DiceErasureSettings : ScriptableObject
    {
        [Header("Sink Erasure (Bottom) — ticks @ SimTiming.TickHz")]
        [FormerlySerializedAs("sinkDuration")]
        [FormerlySerializedAs("dissolveDuration")]
        [Min(1)]
        [SerializeField] int sinkDurationTicks = 48;
        [FormerlySerializedAs("dissolveGhostThreshold")]
        [SerializeField] float sinkGhostThreshold = 0.45f;
        [FormerlySerializedAs("dissolveGhostAlpha")]
        [SerializeField] float sinkGhostAlpha = 0.35f;

        [Header("Radiance Erasure (Top) — ticks @ SimTiming.TickHz")]
        [FormerlySerializedAs("radianceDuration")]
        [Min(1)]
        [SerializeField] int radianceDurationTicks = 24;
        [FormerlySerializedAs("radianceRampUpDuration")]
        [Min(0)]
        [SerializeField] int radianceRampUpDurationTicks = 6;

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

        public int SinkDurationTicks => Mathf.Max(1, sinkDurationTicks);
        public int RadianceDurationTicks => Mathf.Max(1, radianceDurationTicks);
        public int RadianceRampUpDurationTicks => Mathf.Max(0, radianceRampUpDurationTicks);

        /// <summary>Unity / coroutine path (seconds).</summary>
        public float SinkDuration => SimTiming.TicksToSeconds(SinkDurationTicks);
        public float RadianceDuration => SimTiming.TicksToSeconds(RadianceDurationTicks);
        public float RadianceRampUpDuration => SimTiming.TicksToSeconds(RadianceRampUpDurationTicks);

        public float SinkGhostThreshold => sinkGhostThreshold;
        public float SinkGhostAlpha => sinkGhostAlpha;
        public float ErasureEmissionIntensity => erasureEmissionIntensity;
        public Texture ErasureEmissionMap => erasureEmissionMap;
        public float ErasureEmissionPulseSpeed => erasureEmissionPulseSpeed;
        public float ErasureEmissionPulseMin => erasureEmissionPulseMin;
        public float ErasureEmissionPulseMax => erasureEmissionPulseMax;
        public Color NeutralEmissionColor => neutralEmissionColor;

        public Color GetPlayerEmissionColor(PlayerSlot slot)
        {
            return slot == PlayerSlot.Player1 ? player1EmissionColor : player2EmissionColor;
        }

        void OnValidate()
        {
            sinkDurationTicks = Mathf.Max(1, sinkDurationTicks);
            sinkGhostThreshold = Mathf.Clamp01(sinkGhostThreshold);
            sinkGhostAlpha = Mathf.Clamp01(sinkGhostAlpha);
            radianceDurationTicks = Mathf.Max(1, radianceDurationTicks);
            radianceRampUpDurationTicks = Mathf.Max(0, radianceRampUpDurationTicks);
            erasureEmissionIntensity = Mathf.Max(0f, erasureEmissionIntensity);
            erasureEmissionPulseMin = Mathf.Max(0f, erasureEmissionPulseMin);
            erasureEmissionPulseMax = Mathf.Max(erasureEmissionPulseMin, erasureEmissionPulseMax);
        }
    }
}
