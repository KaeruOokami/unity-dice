using UnityEngine;
using UnityEngine.Serialization;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceAnimationSettings", menuName = "Dice/Dice Animation Settings")]
    public class DiceAnimationSettings : ScriptableObject
    {
        [Header("Durations — ticks @ SimTiming.TickHz")]
        [FormerlySerializedAs("rollAnimationDuration")]
        [Min(1)]
        [SerializeField] int rollAnimationDurationTicks = 18;
        [FormerlySerializedAs("jumpParallelRollDurationPerCell")]
        [Min(1)]
        [SerializeField] int jumpParallelRollDurationPerCellTicks = 18;
        [FormerlySerializedAs("slideDuration")]
        [Min(1)]
        [SerializeField] int slideDurationTicks = 18;
        [FormerlySerializedAs("fallHorizontalDuration")]
        [Min(1)]
        [SerializeField] int fallHorizontalDurationTicks = 18;
        [FormerlySerializedAs("liftDuration")]
        [Min(1)]
        [SerializeField] int liftDurationTicks = 18;
        [FormerlySerializedAs("placeDuration")]
        [Min(1)]
        [SerializeField] int placeDurationTicks = 18;

        public int RollAnimationDurationTicks => Mathf.Max(1, rollAnimationDurationTicks);
        public int JumpParallelRollDurationPerCellTicks => Mathf.Max(1, jumpParallelRollDurationPerCellTicks);
        public int SlideDurationTicks => Mathf.Max(1, slideDurationTicks);
        public int FallHorizontalDurationTicks => Mathf.Max(1, fallHorizontalDurationTicks);
        public int LiftDurationTicks => Mathf.Max(1, liftDurationTicks);
        public int PlaceDurationTicks => Mathf.Max(1, placeDurationTicks);

        public float RollAnimationDuration => SimTiming.TicksToSeconds(RollAnimationDurationTicks);
        public float JumpParallelRollDurationPerCell => SimTiming.TicksToSeconds(JumpParallelRollDurationPerCellTicks);
        public float SlideDuration => SimTiming.TicksToSeconds(SlideDurationTicks);
        public float FallHorizontalDuration => SimTiming.TicksToSeconds(FallHorizontalDurationTicks);
        public float LiftDuration => SimTiming.TicksToSeconds(LiftDurationTicks);
        public float PlaceDuration => SimTiming.TicksToSeconds(PlaceDurationTicks);

        public float GetGroundParallelRollDuration(int distance)
        {
            return RollAnimationDuration * Mathf.Max(1, distance);
        }

        public int GetGroundParallelRollDurationTicks(int distance)
        {
            return RollAnimationDurationTicks * Mathf.Max(1, distance);
        }

        public float GetJumpParallelRollDuration(int distance)
        {
            return JumpParallelRollDurationPerCell * Mathf.Max(1, distance);
        }

        void OnValidate()
        {
            rollAnimationDurationTicks = Mathf.Max(1, rollAnimationDurationTicks);
            jumpParallelRollDurationPerCellTicks = Mathf.Max(1, jumpParallelRollDurationPerCellTicks);
            slideDurationTicks = Mathf.Max(1, slideDurationTicks);
            fallHorizontalDurationTicks = Mathf.Max(1, fallHorizontalDurationTicks);
            liftDurationTicks = Mathf.Max(1, liftDurationTicks);
            placeDurationTicks = Mathf.Max(1, placeDurationTicks);
        }
    }
}
