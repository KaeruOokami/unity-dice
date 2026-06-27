using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "PhysicsSettings", menuName = "Dice/Physics Settings")]
    public class PhysicsSettings : ScriptableObject
    {
        [SerializeField] float gravity = GravityMotion.DefaultGravity;
        [SerializeField] float jumpHeightFallback = 1f;
        [SerializeField, Range(1f, 1.2f)] float jumpHeightDiceMultiplier = 1.2f;

        public float Gravity => gravity;
        public float JumpHeightFallback => jumpHeightFallback;
        public float JumpHeightDiceMultiplier => jumpHeightDiceMultiplier;
    }
}
