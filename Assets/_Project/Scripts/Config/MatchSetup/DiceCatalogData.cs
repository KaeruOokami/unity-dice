using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct DiceCatalogEntryData
    {
        public DiceKind Kind;
        public GameObject MeshPrefab;
        public float SpawnWeight;
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
                    MeshPrefab = source[i].MeshPrefab,
                    SpawnWeight = source[i].SpawnWeight
                };
            }

            return new DiceCatalogData { Entries = entries };
        }

        public static DiceCatalogData Empty() {
            return new DiceCatalogData { Entries = Array.Empty<DiceCatalogEntryData>() };
        }

        public DiceCatalogData WithMeshesFrom(DiceCatalog meshSource) {
            if (Entries == null || Entries.Length == 0) {
                return Empty();
            }

            var result = new DiceCatalogEntryData[Entries.Length];
            for (var i = 0; i < Entries.Length; i++) {
                var entry = Entries[i];
                GameObject mesh = null;
                if (meshSource != null) {
                    meshSource.TryGetMeshPrefab(entry.Kind, out mesh);
                }

                result[i] = new DiceCatalogEntryData {
                    Kind = entry.Kind,
                    MeshPrefab = mesh ?? entry.MeshPrefab,
                    SpawnWeight = entry.SpawnWeight
                };
            }

            return new DiceCatalogData { Entries = result };
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

                if (entry.SpawnWeight > 0f && entry.MeshPrefab != null) {
                    hasEligible = true;
                }
            }

            if (!hasEligible) {
                errorMessage = "DiceCatalog: At least one entry with SpawnWeight > 0 and a MeshPrefab is required.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
