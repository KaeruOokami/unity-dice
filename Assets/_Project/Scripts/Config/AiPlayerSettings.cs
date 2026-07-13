using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "AiPlayerSettings", menuName = "Dice/AI Player Settings")]
    public sealed class AiPlayerSettings : ScriptableObject
    {
        [SerializeField] bool player1IsAi;
        [SerializeField] bool player2IsAi = true;
        [SerializeField] float minReplanInterval = 0.3f;
        [SerializeField] float idleReplanInterval = 0.8f;
        [SerializeField] float failedReplanInterval = 1.2f;
        [SerializeField] int moveActionMaxFrames = 36;
        [SerializeField] int jumpMoveMaxFrames = 48;
        [SerializeField] int faceBeforeLiftFrames = 4;
        [SerializeField] bool allowJump = true;
        [SerializeField] float faceValueWeight = 10f;
        [SerializeField] float clusterProgressWeight = 8f;
        [SerializeField] float immediateMatchBonus = 25f;
        [SerializeField] float playerDistancePenalty = 1.5f;
        [SerializeField] float immovableClusterPenalty = 20f;
        [SerializeField] float clusterSizeWeight = 100f;
        [SerializeField] float clusterCompactnessWeight = 50f;
        [SerializeField] float sinkingChainBonus = 200f;
        [SerializeField] float sinkingChainImmediateBonus = 50f;
        [SerializeField] float sinkingChainWorkDieWeight = 8f;
        [SerializeField] float goalSwitchMargin = 8f;
        [SerializeField] int pathSearchMaxSteps = 64;
        [SerializeField] float cellCenterTolerance = 0.08f;
        [SerializeField] int rollStepMaxFrames = 120;

        public bool Player1IsAi => player1IsAi;
        public bool Player2IsAi => player2IsAi;
        public float MinReplanInterval => minReplanInterval;
        public float IdleReplanInterval => idleReplanInterval;
        public float FailedReplanInterval => failedReplanInterval;
        public int MoveActionMaxFrames => moveActionMaxFrames;
        public int JumpMoveMaxFrames => jumpMoveMaxFrames;
        public int FaceBeforeLiftFrames => faceBeforeLiftFrames;
        public bool AllowJump => allowJump;
        public float FaceValueWeight => faceValueWeight;
        public float ClusterProgressWeight => clusterProgressWeight;
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

        public bool IsAiControlled(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1IsAi : player2IsAi;
        }
    }
}
