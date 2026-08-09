using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Continuous Spawn — ticks @ SimTiming.TickHz")]
        [SerializeField] bool continuousSpawnEnabled = true;
        [FormerlySerializedAs("spawnInterval")]
        [Min(0)]
        [SerializeField] int spawnIntervalTicks = 120;
        [FormerlySerializedAs("spawnIntervalJitter")]
        [Min(0)]
        [SerializeField] int spawnIntervalJitterTicks = 30;
        [Range(0f, 1f)]
        [SerializeField] float bottomSpawnWeight = 0.5f;

        public int InitialDiceCount => initialDiceCount;
        public bool AnimateInitialDiceSpawn => animateInitialDiceSpawn;
        public bool ContinuousSpawnEnabled => continuousSpawnEnabled;
        public int SpawnIntervalTicks => Mathf.Max(0, spawnIntervalTicks);
        public int SpawnIntervalJitterTicks => Mathf.Max(0, spawnIntervalJitterTicks);
        public float SpawnInterval => SimTiming.TicksToSeconds(SpawnIntervalTicks);
        public float SpawnIntervalJitter => SimTiming.TicksToSeconds(SpawnIntervalJitterTicks);
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
            spawnIntervalTicks = Mathf.Max(0, data.SpawnIntervalTicks);
            spawnIntervalJitterTicks = Mathf.Max(0, data.SpawnIntervalJitterTicks);
            bottomSpawnWeight = Mathf.Clamp01(data.BottomSpawnWeight);
        }

        public void SetInitialDiceCount(int count) {
            initialDiceCount = Mathf.Max(1, count);
        }

        void OnValidate() {
            spawnIntervalTicks = Mathf.Max(0, spawnIntervalTicks);
            spawnIntervalJitterTicks = Mathf.Max(0, spawnIntervalJitterTicks);
            bottomSpawnWeight = Mathf.Clamp01(bottomSpawnWeight);
        }
    }
}
