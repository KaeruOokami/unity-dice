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
        [SerializeField] float spawnAppearLogicalDuration = 0.75f;

        public float RollAnimationDuration => rollAnimationDuration;
        public float JumpParallelRollDurationPerCell => jumpParallelRollDurationPerCell;
        public float SlideDuration => slideDuration;
        public float FallHorizontalDuration => fallHorizontalDuration;
        public float LiftDuration => liftDuration;
        public float PlaceDuration => placeDuration;
        /// <summary>
        /// Lockstep logical busy for spawn appear (independent of visual bounce length).
        /// </summary>
        public float SpawnAppearLogicalDuration => spawnAppearLogicalDuration;

        public float GetGroundParallelRollDuration(int distance) {
            return rollAnimationDuration * Mathf.Max(1, distance);
        }

        public float GetJumpParallelRollDuration(int distance) {
            return jumpParallelRollDurationPerCell * Mathf.Max(1, distance);
        }
    }
}
