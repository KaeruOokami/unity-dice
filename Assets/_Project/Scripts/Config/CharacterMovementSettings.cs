using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "CharacterMovementSettings", menuName = "Dice/Character Movement Settings")]
    public class CharacterMovementSettings : ScriptableObject
    {
        [SerializeField] float characterHeightOffset = 0.15f;
        [SerializeField] float maxMoveSpeed = 2.5f;
        [SerializeField] float moveAcceleration = 10f;
        [SerializeField] float rollCenterPullSpeed = 2.5f;
        [SerializeField] float maxStepHeight = 1.5f;
        [SerializeField] float pushHoldDuration = 0.25f;
        [SerializeField] float dissolveDescentHoldDuration = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] float rollCancelWindowProgress = 0.1f;
        [SerializeField] float pushInputAlignment = 0.7f;
        [SerializeField] KeyCode liftKey = KeyCode.Q;
        [SerializeField] float carryVerticalOffset = 1.05f;
        [SerializeField] KeyCode jumpKey = KeyCode.Space;
        [SerializeField] bool debugMovementBlock;
        [SerializeField] bool debugPush;
        [SerializeField] bool debugJumpParallelRoll;
        [SerializeField] bool debugJump;

        public float CharacterHeightOffset => characterHeightOffset;
        public float MaxMoveSpeed => maxMoveSpeed;
        public float MoveAcceleration => moveAcceleration;
        public float RollCenterPullSpeed => rollCenterPullSpeed;
        public float MaxStepHeight => maxStepHeight;
        public float PushHoldDuration => pushHoldDuration;
        public float DissolveDescentHoldDuration => dissolveDescentHoldDuration;
        public float RollCancelWindowProgress => rollCancelWindowProgress;
        public float PushInputAlignment => pushInputAlignment;
        public KeyCode LiftKey => liftKey;
        public float CarryVerticalOffset => carryVerticalOffset;
        public KeyCode JumpKey => jumpKey;
        public bool DebugMovementBlock => debugMovementBlock;
        public bool DebugPush => debugPush;
        public bool DebugJumpParallelRoll => debugJumpParallelRoll;
        public bool DebugJump => debugJump;
    }
}
