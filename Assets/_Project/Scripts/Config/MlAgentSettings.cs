using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "MlAgentSettings", menuName = "Dice/ML Agent Settings")]
    public sealed class MlAgentSettings : ScriptableObject
    {
        [Header("Behavior")]
        [SerializeField] string behaviorName = "DiceBehavior";
        [SerializeField] int decisionPeriod = 5;
        [SerializeField] int maxEpisodeSteps = 5000;
        [SerializeField] int maxObservedDice = 16;

        [Header("Rewards")]
        [SerializeField] float stepPenalty = -0.001f;
        [SerializeField] float standingOnDieReward = 0.001f;
        [SerializeField] float winReward = 1f;
        [SerializeField] float loseReward = -1f;
        [SerializeField] float drawReward = -0.2f;
        [SerializeField] float timeoutPenalty = -0.5f;

        [Header("Debug")]
        [SerializeField] bool debugLog;
        [SerializeField] bool debugGizmo = true;

        public string BehaviorName => behaviorName;
        public int DecisionPeriod => Mathf.Max(1, decisionPeriod);
        public int MaxEpisodeSteps => Mathf.Max(0, maxEpisodeSteps);
        public int MaxObservedDice => Mathf.Max(0, maxObservedDice);
        public float StepPenalty => stepPenalty;
        public float StandingOnDieReward => standingOnDieReward;
        public float WinReward => winReward;
        public float LoseReward => loseReward;
        public float DrawReward => drawReward;
        public float TimeoutPenalty => timeoutPenalty;
        public bool DebugLog => debugLog;
        public bool DebugGizmo => debugGizmo;
    }
}
