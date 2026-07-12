using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "CharacterMovementSettings", menuName = "Dice/Character Movement Settings")]
    public class CharacterMovementSettings : ScriptableObject
    {
        [SerializeField] float characterHeightOffset = 0.15f;
        [SerializeField] float maxMoveSpeed = 2.5f;
        [SerializeField] float moveAcceleration = 10f;
        [Range(0.05f, 1f)]
        [SerializeField] float rollTriggerExtentRatio = 0.35f;
        [SerializeField] float maxWalkStep = 0.5f;
        [SerializeField] float maxJumpStepPlayerOnly = 0.5f;
        [SerializeField] float maxJumpStepCoupled = 1f;
        [SerializeField] float pushHoldDuration = 0.25f;
        [SerializeField] float dissolveDescentHoldDuration = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] float rollCancelWindowProgress = 0.1f;
        [SerializeField] float pushInputAlignment = 0.7f;
        [SerializeField] float carryVerticalOffset = 1.05f;
        [SerializeField] bool debugMovementBlock;
        [SerializeField] bool debugPush;
        [SerializeField] bool debugJumpParallelRoll;
        [SerializeField] bool debugJump;

        public float CharacterHeightOffset => characterHeightOffset;
        public float MaxMoveSpeed => maxMoveSpeed;
        public float MoveAcceleration => moveAcceleration;
        public float RollTriggerExtentRatio => rollTriggerExtentRatio;
        public float MaxWalkStep => maxWalkStep;

        public float GetRollTriggerHalfExtent(float walkHalfExtent) {
            return walkHalfExtent * rollTriggerExtentRatio;
        }
        public float MaxJumpStepPlayerOnly => maxJumpStepPlayerOnly;
        public float MaxJumpStepCoupled => maxJumpStepCoupled;
        public float PushHoldDuration => pushHoldDuration;
        public float DissolveDescentHoldDuration => dissolveDescentHoldDuration;
        public float RollCancelWindowProgress => rollCancelWindowProgress;
        public float PushInputAlignment => pushInputAlignment;
        public float CarryVerticalOffset => carryVerticalOffset;
        public bool DebugMovementBlock => debugMovementBlock;
        public bool DebugPush => debugPush;
        public bool DebugJumpParallelRoll => debugJumpParallelRoll;
        public bool DebugJump => debugJump;
    }
}
