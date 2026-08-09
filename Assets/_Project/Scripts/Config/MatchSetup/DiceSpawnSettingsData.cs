using UnityEngine;

namespace DiceGame.Config
{
    public struct DiceSpawnSettingsData
    {
        public int InitialDiceCount;
        public bool AnimateInitialDiceSpawn;
        public bool ContinuousSpawnEnabled;
        public int SpawnIntervalTicks;
        public int SpawnIntervalJitterTicks;
        public float BottomSpawnWeight;

        public static DiceSpawnSettingsData FromTemplate(DiceSpawnSettings template) {
            if (template == null) {
                return Default();
            }

            return new DiceSpawnSettingsData {
                InitialDiceCount = template.InitialDiceCount,
                AnimateInitialDiceSpawn = template.AnimateInitialDiceSpawn,
                ContinuousSpawnEnabled = template.ContinuousSpawnEnabled,
                SpawnIntervalTicks = template.SpawnIntervalTicks,
                SpawnIntervalJitterTicks = template.SpawnIntervalJitterTicks,
                BottomSpawnWeight = template.BottomSpawnWeight
            };
        }

        public static DiceSpawnSettingsData Default() {
            return new DiceSpawnSettingsData {
                InitialDiceCount = 3,
                AnimateInitialDiceSpawn = true,
                ContinuousSpawnEnabled = true,
                SpawnIntervalTicks = 120,
                SpawnIntervalJitterTicks = 30,
                BottomSpawnWeight = 0.5f
            };
        }

        public DiceSpawnSettings ToRuntimeAsset() {
            return DiceSpawnSettings.CreateRuntime(this);
        }

        public DiceSpawnSettingsData WithInitialDiceCount(int count) {
            var copy = this;
            copy.InitialDiceCount = Mathf.Max(1, count);
            return copy;
        }

        public bool TryValidate(out string errorMessage) {
            if (InitialDiceCount < 1) {
                errorMessage = "DiceSpawnSettings: InitialDiceCount must be at least 1.";
                return false;
            }

            if (SpawnIntervalTicks < 0) {
                errorMessage = "DiceSpawnSettings: SpawnIntervalTicks must be non-negative.";
                return false;
            }

            if (SpawnIntervalJitterTicks < 0) {
                errorMessage = "DiceSpawnSettings: SpawnIntervalJitterTicks must be non-negative.";
                return false;
            }

            if (BottomSpawnWeight < 0f || BottomSpawnWeight > 1f) {
                errorMessage = "DiceSpawnSettings: BottomSpawnWeight must be between 0 and 1.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
