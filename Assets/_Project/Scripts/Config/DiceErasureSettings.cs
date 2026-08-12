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

        [Header("Default Player Emission (Single / Coop)")]
        [FormerlySerializedAs("erasureEmissionColor")]
        [SerializeField] Color player1EmissionColor = new(0.2f, 0.6f, 1f, 1f);
        [SerializeField] Color player2EmissionColor = new(1f, 0.35f, 0.2f, 1f);

        [Header("Face Emission Palette (Attack FaceWeight / Versus)")]
        [SerializeField] Color face2EmissionColor = new(1f, 0.92f, 0.15f, 1f);
        [SerializeField] Color face3EmissionColor = new(0.45f, 0.25f, 1f, 1f);
        [SerializeField] Color face4EmissionColor = new(0.25f, 0.85f, 1f, 1f);
        [SerializeField] Color face5EmissionColor = new(0.2f, 1f, 0.35f, 1f);
        [SerializeField] Color face6EmissionColor = new(1f, 0.2f, 0.75f, 1f);

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
        public Color Face2EmissionColor => face2EmissionColor;
        public Color Face3EmissionColor => face3EmissionColor;
        public Color Face4EmissionColor => face4EmissionColor;
        public Color Face5EmissionColor => face5EmissionColor;
        public Color Face6EmissionColor => face6EmissionColor;

        public Color GetDefaultPlayerEmissionColor(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1EmissionColor : player2EmissionColor;
        }

        public Color ResolvePlayerEmissionColor(PlayerAttackSettings attack) {
            if (attack == null) {
                Debug.LogError("DiceErasureSettings: PlayerAttackSettings is required to resolve emission color.");
                return NeutralEmissionColor;
            }

            return AttackFaceEmissionColorResolver.Resolve(
                neutralEmissionColor,
                face2EmissionColor,
                face3EmissionColor,
                face4EmissionColor,
                face5EmissionColor,
                face6EmissionColor,
                attack.GetFaceWeight(2),
                attack.GetFaceWeight(3),
                attack.GetFaceWeight(4),
                attack.GetFaceWeight(5),
                attack.GetFaceWeight(6));
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
