using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceAnimationSettings", menuName = "Dice/Dice Animation Settings")]
    public class DiceAnimationSettings : ScriptableObject
    {
        [SerializeField] float rollAnimationDuration = 0.3f;
        [SerializeField] float jumpParallelRollDurationPerCell = 0.3f;
        [SerializeField] float slideDuration = 0.3f;
        [SerializeField] float fallHorizontalDuration = 0.3f;
        [SerializeField] float liftDuration = 0.3f;
        [SerializeField] float placeDuration = 0.3f;

        public float RollAnimationDuration => rollAnimationDuration;
        public float JumpParallelRollDurationPerCell => jumpParallelRollDurationPerCell;
        public float SlideDuration => slideDuration;
        public float FallHorizontalDuration => fallHorizontalDuration;
        public float LiftDuration => liftDuration;
        public float PlaceDuration => placeDuration;

        public float GetGroundParallelRollDuration(int distance) {
            return rollAnimationDuration * Mathf.Max(1, distance);
        }

        public float GetJumpParallelRollDuration(int distance) {
            return jumpParallelRollDurationPerCell * Mathf.Max(1, distance);
        }
    }
}
