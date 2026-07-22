using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceSpawnSettings", menuName = "Dice/Dice Spawn Settings")]
    public class DiceSpawnSettings : ScriptableObject
    {
        [Header("Initial (random Bottom / Top placement)")]
        [Tooltip("For Versus, prefer VersusBoardSettings.Shared Initial Dice Count (synced to 1P/2P).")]
        [Min(1)]
        [SerializeField] int initialDiceCount = 3;
        [SerializeField] bool animateInitialDiceSpawn = true;

        [Header("Continuous Spawn")]
        [SerializeField] bool continuousSpawnEnabled = true;
        [SerializeField] float spawnInterval = 2f;
        [SerializeField] float spawnIntervalJitter = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] float bottomSpawnWeight = 0.5f;

        public int InitialDiceCount => initialDiceCount;
        public bool AnimateInitialDiceSpawn => animateInitialDiceSpawn;
        public bool ContinuousSpawnEnabled => continuousSpawnEnabled;
        public float SpawnInterval => spawnInterval;
        public float SpawnIntervalJitter => spawnIntervalJitter;
        public float BottomSpawnWeight => bottomSpawnWeight;
        public float TopSpawnWeight => 1f - bottomSpawnWeight;

        public static DiceSpawnSettings CreateRuntime(DiceSpawnSettingsData data) {
            var instance = CreateInstance<DiceSpawnSettings>();
            instance.Apply(data);
            return instance;
        }

        public void Apply(DiceSpawnSettingsData data) {
            SetInitialDiceCount(data.InitialDiceCount);
            animateInitialDiceSpawn = data.AnimateInitialDiceSpawn;
            continuousSpawnEnabled = data.ContinuousSpawnEnabled;
            spawnInterval = Mathf.Max(0f, data.SpawnInterval);
            spawnIntervalJitter = Mathf.Max(0f, data.SpawnIntervalJitter);
            bottomSpawnWeight = Mathf.Clamp01(data.BottomSpawnWeight);
        }

        public void SetInitialDiceCount(int count) {
            initialDiceCount = Mathf.Max(1, count);
        }
    }
}
