using UnityEngine;
using UnityEngine.Serialization;

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
        [Header("Hold durations — ticks @ SimTiming.TickHz")]
        [FormerlySerializedAs("pushHoldDuration")]
        [Min(1)]
        [SerializeField] int pushHoldDurationTicks = 15;
        [FormerlySerializedAs("dissolveDescentHoldDuration")]
        [Min(1)]
        [SerializeField] int dissolveDescentHoldDurationTicks = 21;
        [Min(0)]
        [SerializeField] int pushContactRadiusMilli = 250;
        [Range(0f, 1f)]
        [SerializeField] float rollCancelWindowProgress = 0.1f;
        [SerializeField] float pushInputAlignment = 0.7f;
        [SerializeField] float carryVerticalOffset = 1.05f;
        [Range(0f, 1f)]
        [SerializeField] float jumpLandingSinkAdvance = 0.1f;
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
        public int PushHoldDurationTicks => Mathf.Max(1, pushHoldDurationTicks);
        public int DissolveDescentHoldDurationTicks => Mathf.Max(1, dissolveDescentHoldDurationTicks);
        public int PushContactRadiusMilli => Mathf.Max(0, pushContactRadiusMilli);
        public float PushHoldDuration => SimTiming.TicksToSeconds(PushHoldDurationTicks);
        public float DissolveDescentHoldDuration => SimTiming.TicksToSeconds(DissolveDescentHoldDurationTicks);
        public float RollCancelWindowProgress => rollCancelWindowProgress;
        public float PushInputAlignment => pushInputAlignment;
        public float CarryVerticalOffset => carryVerticalOffset;
        public float JumpLandingSinkAdvance => jumpLandingSinkAdvance;
        public bool DebugMovementBlock => debugMovementBlock;
        public bool DebugPush => debugPush;
        public bool DebugJumpParallelRoll => debugJumpParallelRoll;
        public bool DebugJump => debugJump;

        void OnValidate() {
            jumpLandingSinkAdvance = Mathf.Clamp01(jumpLandingSinkAdvance);
            pushHoldDurationTicks = Mathf.Max(1, pushHoldDurationTicks);
            dissolveDescentHoldDurationTicks = Mathf.Max(1, dissolveDescentHoldDurationTicks);
            pushContactRadiusMilli = Mathf.Max(0, pushContactRadiusMilli);
        }
    }
}
