using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceAnimationSettings", menuName = "Dice/Dice Animation Settings")]
    public class DiceAnimationSettings : ScriptableObject
    {
        [SerializeField] float rollAnimationDuration = 0.3f;
        [SerializeField] float slideDuration = 0.3f;
        [SerializeField] float fallHorizontalDuration = 0.3f;
        [SerializeField] float liftDuration = 0.3f;
        [SerializeField] float placeDuration = 0.3f;

        public float RollAnimationDuration => rollAnimationDuration;
        public float SlideDuration => slideDuration;
        public float FallHorizontalDuration => fallHorizontalDuration;
        public float LiftDuration => liftDuration;
        public float PlaceDuration => placeDuration;
    }
}
