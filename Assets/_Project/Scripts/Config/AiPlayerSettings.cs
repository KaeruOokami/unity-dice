using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "AiPlayerSettings", menuName = "Dice/AI Player Settings")]
    public sealed class AiPlayerSettings : ScriptableObject
    {
        [Header("Replan Timing")]
        [SerializeField] float minReplanInterval = 0.3f;
        [SerializeField] float idleReplanInterval = 0.8f;
        [SerializeField] float failedReplanInterval = 1.2f;

        [Header("Action Limits")]
        [SerializeField] int moveActionMaxFrames = 36;
        [SerializeField] int jumpMoveMaxFrames = 48;
        [SerializeField] int faceBeforeLiftFrames = 4;
        [SerializeField] int rollStepMaxFrames = 120;
        [SerializeField] bool allowJump = true;

        [Header("Navigation")]
        [SerializeField] int pathSearchMaxSteps = 64;
        [SerializeField] float cellCenterTolerance = 0.08f;

        [Header("Goal Scoring")]
        [SerializeField] float faceValueWeight = 10f;
        [SerializeField] float clusterProgressWeight = 8f;
        [SerializeField] float sameFaceProximityWeight = 20f;
        [SerializeField] float clusterSizeWeight = 100f;
        [SerializeField] float clusterCompactnessWeight = 50f;
        [SerializeField] float playerDistancePenalty = 1.5f;
        [SerializeField] float immovableClusterPenalty = 20f;
        [SerializeField] float immediateMatchBonus = 25f;

        [Header("Sinking Chain Scoring")]
        [SerializeField] float sinkingChainBonus = 200f;
        [SerializeField] float sinkingChainImmediateBonus = 50f;
        [SerializeField] float sinkingChainWorkDieWeight = 8f;

        [Header("Goal Persistence")]
        [SerializeField] float goalSwitchMargin = 8f;
        [SerializeField] int stuckAttemptsBeforeGoalReset = 3;
        [SerializeField] float goalFailureBlacklistSeconds = 12f;

        [Header("Debug")]
        [SerializeField] bool debugLog;
        [SerializeField] bool debugGizmo;

        public float MinReplanInterval => minReplanInterval;
        public float IdleReplanInterval => idleReplanInterval;
        public float FailedReplanInterval => failedReplanInterval;
        public int MoveActionMaxFrames => moveActionMaxFrames;
        public int JumpMoveMaxFrames => jumpMoveMaxFrames;
        public int FaceBeforeLiftFrames => faceBeforeLiftFrames;
        public bool AllowJump => allowJump;
        public float FaceValueWeight => faceValueWeight;
        public float ClusterProgressWeight => clusterProgressWeight;
        public float SameFaceProximityWeight => sameFaceProximityWeight;
        public float ImmediateMatchBonus => immediateMatchBonus;
        public float PlayerDistancePenalty => playerDistancePenalty;
        public float ImmovableClusterPenalty => immovableClusterPenalty;
        public float ClusterSizeWeight => clusterSizeWeight;
        public float ClusterCompactnessWeight => clusterCompactnessWeight;
        public float SinkingChainBonus => sinkingChainBonus;
        public float SinkingChainImmediateBonus => sinkingChainImmediateBonus;
        public float SinkingChainWorkDieWeight => sinkingChainWorkDieWeight;
        public float GoalSwitchMargin => goalSwitchMargin;
        public int PathSearchMaxSteps => pathSearchMaxSteps;
        public float CellCenterTolerance => cellCenterTolerance;
        public int RollStepMaxFrames => rollStepMaxFrames;
        public int StuckAttemptsBeforeGoalReset => stuckAttemptsBeforeGoalReset;
        public float GoalFailureBlacklistSeconds => goalFailureBlacklistSeconds;
        public bool DebugLog => debugLog;
        public bool DebugGizmo => debugGizmo;
    }
}
