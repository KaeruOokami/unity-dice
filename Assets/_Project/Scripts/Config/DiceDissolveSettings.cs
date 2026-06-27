using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceDissolveSettings", menuName = "Dice/Dice Dissolve Settings")]
    public class DiceDissolveSettings : ScriptableObject
    {
        [SerializeField] float dissolveDuration = 0.8f;
        [SerializeField] float dissolveGhostThreshold = 0.45f;
        [SerializeField] float dissolveGhostAlpha = 0.35f;
        [SerializeField] Color dissolveEmissionColor = new Color(0.4f, 0.8f, 1f, 1f);
        [SerializeField] float dissolveEmissionIntensity = 2f;
        [SerializeField] Texture dissolveEmissionMap;
        [SerializeField] float dissolveEmissionPulseSpeed = 4f;
        [SerializeField] float dissolveEmissionPulseMin = 0.55f;
        [SerializeField] float dissolveEmissionPulseMax = 1f;

        public float DissolveDuration => dissolveDuration;
        public float DissolveGhostThreshold => dissolveGhostThreshold;
        public float DissolveGhostAlpha => dissolveGhostAlpha;
        public Color DissolveEmissionColor => dissolveEmissionColor;
        public float DissolveEmissionIntensity => dissolveEmissionIntensity;
        public Texture DissolveEmissionMap => dissolveEmissionMap;
        public float DissolveEmissionPulseSpeed => dissolveEmissionPulseSpeed;
        public float DissolveEmissionPulseMin => dissolveEmissionPulseMin;
        public float DissolveEmissionPulseMax => dissolveEmissionPulseMax;
    }
}
