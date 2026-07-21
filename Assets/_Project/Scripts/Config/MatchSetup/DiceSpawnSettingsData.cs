using UnityEngine;

namespace DiceGame.Config
{
    public struct DiceSpawnSettingsData
    {
        public int InitialDiceCount;
        public bool AnimateInitialDiceSpawn;
        public bool ContinuousSpawnEnabled;
        public float SpawnInterval;
        public float SpawnIntervalJitter;
        public float BottomSpawnWeight;

        public static DiceSpawnSettingsData FromTemplate(DiceSpawnSettings template) {
            if (template == null) {
                return Default();
            }

            return new DiceSpawnSettingsData {
                InitialDiceCount = template.InitialDiceCount,
                AnimateInitialDiceSpawn = template.AnimateInitialDiceSpawn,
                ContinuousSpawnEnabled = template.ContinuousSpawnEnabled,
                SpawnInterval = template.SpawnInterval,
                SpawnIntervalJitter = template.SpawnIntervalJitter,
                BottomSpawnWeight = template.BottomSpawnWeight
            };
        }

        public static DiceSpawnSettingsData Default() {
            return new DiceSpawnSettingsData {
                InitialDiceCount = 3,
                AnimateInitialDiceSpawn = true,
                ContinuousSpawnEnabled = true,
                SpawnInterval = 2f,
                SpawnIntervalJitter = 0.5f,
                BottomSpawnWeight = 0.5f
            };
        }

        public DiceSpawnSettings ToRuntimeAsset() {
            return DiceSpawnSettings.CreateRuntime(this);
        }

        public bool TryValidate(out string errorMessage) {
            if (InitialDiceCount < 1) {
                errorMessage = "DiceSpawnSettings: InitialDiceCount must be at least 1.";
                return false;
            }

            if (SpawnInterval < 0f) {
                errorMessage = "DiceSpawnSettings: SpawnInterval must be non-negative.";
                return false;
            }

            if (SpawnIntervalJitter < 0f) {
                errorMessage = "DiceSpawnSettings: SpawnIntervalJitter must be non-negative.";
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
