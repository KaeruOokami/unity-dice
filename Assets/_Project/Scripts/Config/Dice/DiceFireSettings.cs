using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceFireSettings", menuName = "Dice/Dice/Fire Settings")]
    public class DiceFireSettings : ScriptableObject
    {
        [Min(0.01f)]
        [SerializeField] float duration = 8f;
        [Min(0.01f)]
        [SerializeField] float spreadInterval = 1f;
        [Range(0f, 1f)]
        [SerializeField] float spreadChance = 0.25f;
        [Min(0f)]
        [SerializeField] float playerStunDuration = 1.5f;

        public float Duration => Mathf.Max(0.01f, duration);
        public float SpreadInterval => Mathf.Max(0.01f, spreadInterval);
        public float SpreadChance => Mathf.Clamp01(spreadChance);
        public float PlayerStunDuration => Mathf.Max(0f, playerStunDuration);
    }
}
