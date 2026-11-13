using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "ReverseModeSettings", menuName = "Dice/Match/Reverse Mode Settings")]
    public sealed class ReverseModeSettings : ScriptableObject
    {
        [Min(0.01f)]
        [SerializeField] float durationSeconds = 10f;

        public float DurationSeconds => Mathf.Max(0.01f, durationSeconds);
    }
}
