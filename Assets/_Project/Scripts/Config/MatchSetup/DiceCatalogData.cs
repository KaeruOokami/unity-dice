using System;
using DiceGame.Core;

namespace DiceGame.Config
{
    [Serializable]
    public struct DiceCatalogEntryData
    {
        public DiceKind Kind;
        public float SpawnWeight;
        public bool IgniteOnSpawn;
    }

    public struct DiceCatalogData
    {
        public DiceCatalogEntryData[] Entries;

        public static DiceCatalogData FromTemplate(DiceCatalog template) {
            if (template == null || template.Entries == null || template.Entries.Length == 0) {
                return Empty();
            }

            var source = template.Entries;
            var entries = new DiceCatalogEntryData[source.Length];
            for (var i = 0; i < source.Length; i++) {
                entries[i] = new DiceCatalogEntryData {
                    Kind = source[i].Kind,
                    SpawnWeight = source[i].SpawnWeight,
                    IgniteOnSpawn = source[i].IgniteOnSpawn
                };
            }

            return new DiceCatalogData { Entries = entries };
        }

        public static DiceCatalogData Empty() {
            return new DiceCatalogData { Entries = Array.Empty<DiceCatalogEntryData>() };
        }

        public DiceCatalog ToRuntimeAsset() {
            return DiceCatalog.CreateRuntime(this);
        }

        public bool TryValidate(out string errorMessage) {
            if (Entries == null || Entries.Length == 0) {
                errorMessage = "DiceCatalog: At least one entry is required.";
                return false;
            }

            var hasEligible = false;
            for (var i = 0; i < Entries.Length; i++) {
                var entry = Entries[i];
                if (entry.SpawnWeight < 0f) {
                    errorMessage = $"DiceCatalog: Entry[{i}] ({entry.Kind}) SpawnWeight must be non-negative.";
                    return false;
                }

                if (entry.SpawnWeight > 0f) {
                    hasEligible = true;
                }
            }

            if (!hasEligible) {
                errorMessage = "DiceCatalog: At least one entry with SpawnWeight > 0 is required.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
