using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "MatchSeriesSettings", menuName = "Dice/Match/Match Series Settings")]
    public sealed class MatchSeriesSettings : ScriptableObject
    {
        [Header("Series")]
        [Min(1)]
        [SerializeField] int winsToWin = 2;
        [Min(0f)]
        [SerializeField] float roundEndDelaySeconds = 2f;

        [Header("Attack Queue Pacing")]
        [Min(0f)]
        [SerializeField] float queueToBoardDelay = 1.5f;

        public int WinsToWin => Mathf.Max(1, winsToWin);
        public float RoundEndDelaySeconds => Mathf.Max(0f, roundEndDelaySeconds);
        public float QueueToBoardDelay => Mathf.Max(0f, queueToBoardDelay);

        void OnValidate() {
            winsToWin = Mathf.Max(1, winsToWin);
            roundEndDelaySeconds = Mathf.Max(0f, roundEndDelaySeconds);
            queueToBoardDelay = Mathf.Max(0f, queueToBoardDelay);
        }
    }
}
