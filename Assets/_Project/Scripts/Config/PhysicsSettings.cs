using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "PhysicsSettings", menuName = "Dice/Physics Settings")]
    public class PhysicsSettings : ScriptableObject
    {
        [SerializeField] float gravity = GravityMotion.DefaultGravity;
        [SerializeField] float jumpHeightFallback = 1f;
        [SerializeField] float jumpHeightDiceMultiplier = 1.0f;

        [Header("Jump Grid Move Timelines (ascent only, u: 0 = launch, 0.5 = apex)")]
        [SerializeField] float jumpGridMoveTwoCellMaxTimeline = 0.1f;
        [SerializeField] float jumpGridMoveOneCellMaxTimeline = 0.5f;
        [SerializeField] float jumpGridMoveTierChangeMinTimeline = 0.2f;
        [SerializeField] float jumpGridMoveTierChangeMaxTimeline = 0.5f;

        public float Gravity => gravity;
        public float JumpHeightFallback => jumpHeightFallback;
        public float JumpHeightDiceMultiplier => jumpHeightDiceMultiplier;
        public float JumpGridMoveTwoCellMaxTimeline => jumpGridMoveTwoCellMaxTimeline;
        public float JumpGridMoveOneCellMaxTimeline => jumpGridMoveOneCellMaxTimeline;
        public float JumpGridMoveTierChangeMinTimeline => jumpGridMoveTierChangeMinTimeline;
        public float JumpGridMoveTierChangeMaxTimeline => jumpGridMoveTierChangeMaxTimeline;
    }
}
