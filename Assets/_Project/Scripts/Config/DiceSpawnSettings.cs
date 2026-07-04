using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceSpawnSettings", menuName = "Dice/Dice Spawn Settings")]
    public class DiceSpawnSettings : ScriptableObject
    {
        [Header("Initial (random Bottom / Top placement)")]
        [Min(1)]
        [SerializeField] int initialDiceCount = 3;
        [SerializeField] bool animateInitialDiceSpawn = true;

        [Header("Continuous Spawn")]
        [SerializeField] bool continuousSpawnEnabled = true;
        [SerializeField] float spawnInterval = 2f;
        [SerializeField] float spawnIntervalJitter = 0.5f;

        [Header("Bottom Emergence")]
        [SerializeField] float bottomEmergenceDuration = 0.8f;

        [Header("Top Spawn (fall + bounce)")]
        [SerializeField] float spawnHeight = 4f;
        [Range(0f, 1f)]
        [SerializeField] float bounceRestitution = 0.35f;
        [SerializeField] int maxBounceCount = 2;
        [SerializeField] float minBounceVelocity = 2f;

        public int InitialDiceCount => initialDiceCount;
        public bool AnimateInitialDiceSpawn => animateInitialDiceSpawn;
        public bool ContinuousSpawnEnabled => continuousSpawnEnabled;
        public float SpawnInterval => spawnInterval;
        public float SpawnIntervalJitter => spawnIntervalJitter;
        public float BottomEmergenceDuration => bottomEmergenceDuration;
        public float SpawnHeight => spawnHeight;
        public float BounceRestitution => bounceRestitution;
        public int MaxBounceCount => maxBounceCount;
        public float MinBounceVelocity => minBounceVelocity;
    }
}
