using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceCatalog", menuName = "Dice/Dice Catalog")]
    public class DiceCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public DiceKind Kind;
            public GameObject MeshPrefab;
            [Min(0f)] public float SpawnWeight;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;

        public bool TryPickRandomKind(System.Random random, out DiceKind kind) {
            kind = default;
            if (random == null || entries == null || entries.Length == 0) {
                return false;
            }

            var totalWeight = 0f;
            for (var i = 0; i < entries.Length; i++) {
                if (entries[i].SpawnWeight > 0f && entries[i].MeshPrefab != null) {
                    totalWeight += entries[i].SpawnWeight;
                }
            }

            if (totalWeight <= 0f) {
                return false;
            }

            var roll = (float)(random.NextDouble() * totalWeight);
            var cumulative = 0f;
            for (var i = 0; i < entries.Length; i++) {
                var entry = entries[i];
                if (entry.SpawnWeight <= 0f || entry.MeshPrefab == null) {
                    continue;
                }

                cumulative += entry.SpawnWeight;
                if (roll < cumulative) {
                    kind = entry.Kind;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetMeshPrefab(DiceKind kind, out GameObject meshPrefab) {
            meshPrefab = null;
            if (entries == null) {
                return false;
            }

            for (var i = 0; i < entries.Length; i++) {
                if (entries[i].Kind != kind) {
                    continue;
                }

                meshPrefab = entries[i].MeshPrefab;
                return meshPrefab != null;
            }

            return false;
        }

        public bool TryGetEntry(DiceKind kind, out Entry entry) {
            if (entries != null) {
                for (var i = 0; i < entries.Length; i++) {
                    if (entries[i].Kind == kind) {
                        entry = entries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        public static DiceCatalog CreateRuntime(DiceCatalogData data) {
            var instance = CreateInstance<DiceCatalog>();
            instance.Apply(data);
            return instance;
        }

        public void Apply(DiceCatalogData data) {
            if (data.Entries == null || data.Entries.Length == 0) {
                entries = Array.Empty<Entry>();
                return;
            }

            entries = new Entry[data.Entries.Length];
            for (var i = 0; i < data.Entries.Length; i++) {
                entries[i] = new Entry {
                    Kind = data.Entries[i].Kind,
                    MeshPrefab = data.Entries[i].MeshPrefab,
                    SpawnWeight = Mathf.Max(0f, data.Entries[i].SpawnWeight)
                };
            }
        }
    }
}
