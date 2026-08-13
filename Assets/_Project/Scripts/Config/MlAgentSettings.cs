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

        [Header("Step Rewards")]
        [SerializeField] float stepPenalty = -0.001f;

        [Header("Erasure Rewards")]
        [SerializeField] float erasureBaseReward = 0.5f;
        [SerializeField] float erasurePerClusterWeight = 0.15f;
        [SerializeField] float chainBonusPerLink = 0.25f;
        [SerializeField] float snatchBonus = 0.3f;

        [Header("Progress Shaping")]
        [SerializeField] bool progressShapingEnabled = true;
        [SerializeField] float clusterProgressWeight = 0.2f;
        [SerializeField] float clusterSizeProgressWeight = 0.25f;
        [SerializeField] float chainPotentialBonus = 0.1f;
        [SerializeField] float clusterHoldWeight = 0.001f;

        [Header("Terminal Rewards")]
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
        public float StepPenalty => stepPenalty;
        public float ErasureBaseReward => erasureBaseReward;
        public float ErasurePerClusterWeight => erasurePerClusterWeight;
        public float ChainBonusPerLink => chainBonusPerLink;
        public float SnatchBonus => snatchBonus;
        public bool ProgressShapingEnabled => progressShapingEnabled;
        public float ClusterProgressWeight => clusterProgressWeight;
        public float ClusterSizeProgressWeight => clusterSizeProgressWeight;
        public float ChainPotentialBonus => chainPotentialBonus;
        public float ClusterHoldWeight => Mathf.Max(0f, clusterHoldWeight);
        public float WinReward => winReward;
        public float LoseReward => loseReward;
        public float DrawReward => drawReward;
        public float TimeoutPenalty => timeoutPenalty;
        public bool DebugLog => debugLog;
        public bool DebugGizmo => debugGizmo;
    }
}
