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
        [Range(0f, 1f)]
        [SerializeField] float jumpGridMoveTwoCellMaxTimeline = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] float jumpGridMoveOneCellMaxTimeline = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] float jumpGridMoveTierChangeMinTimeline = 0.2f;
        [Range(0f, 1f)]
        [SerializeField] float jumpGridMoveTierChangeMaxTimeline = 0.5f;

        [Header("Bottom Emergence")]
        [SerializeField] float bottomEmergenceDuration = 2.5f;

        [Header("Top Spawn (fall + bounce)")]
        [SerializeField] float spawnHeight = 7f;
        [Range(0f, 1f)]
        [SerializeField] float bounceRestitution = 0.35f;
        [SerializeField] int maxBounceCount = 2;
        [SerializeField] float minBounceVelocity = 2f;

        public float Gravity => gravity;
        public float JumpHeightFallback => jumpHeightFallback;
        public float JumpHeightDiceMultiplier => jumpHeightDiceMultiplier;
        public float JumpGridMoveTwoCellMaxTimeline => jumpGridMoveTwoCellMaxTimeline;
        public float JumpGridMoveOneCellMaxTimeline => jumpGridMoveOneCellMaxTimeline;
        public float JumpGridMoveTierChangeMinTimeline => jumpGridMoveTierChangeMinTimeline;
        public float JumpGridMoveTierChangeMaxTimeline => jumpGridMoveTierChangeMaxTimeline;
        public float BottomEmergenceDuration => bottomEmergenceDuration;
        public float SpawnHeight => spawnHeight;
        public float BounceRestitution => bounceRestitution;
        public int MaxBounceCount => maxBounceCount;
        public float MinBounceVelocity => minBounceVelocity;
    }
}
