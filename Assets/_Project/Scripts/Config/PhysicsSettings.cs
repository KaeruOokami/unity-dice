using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "PhysicsSettings", menuName = "Dice/Physics Settings")]
    public class PhysicsSettings : ScriptableObject
    {
        [SerializeField] float gravity = GravityMotion.DefaultGravity;
        [SerializeField] float jumpHeightFallback = 1f;
        [SerializeField] float jumpHeightDiceMinMultiplier = 0.3f;
        [SerializeField] float jumpHeightDiceMultiplier = 1.0f;
        [SerializeField] float jumpParallelRollTwoCellMaxRatio = 0.1f;

        public float Gravity => gravity;
        public float JumpHeightFallback => jumpHeightFallback;
        public float JumpHeightDiceMinMultiplier => jumpHeightDiceMinMultiplier;
        public float JumpHeightDiceMultiplier => jumpHeightDiceMultiplier;
        public float JumpParallelRollTwoCellMaxRatio => jumpParallelRollTwoCellMaxRatio;
    }
}
