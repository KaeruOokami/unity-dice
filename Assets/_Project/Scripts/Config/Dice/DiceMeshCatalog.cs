using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    /// <summary>
    /// Game-wide Kind → mesh mapping (presentation only).
    /// Spawn weights live in <see cref="DiceCatalog"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "DiceMeshCatalog", menuName = "Dice/Dice/Mesh Catalog")]
    public sealed class DiceMeshCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public DiceKind Kind;
            public GameObject MeshPrefab;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries ?? Array.Empty<Entry>();

        public bool TryGetMeshPrefab(DiceKind kind, out GameObject meshPrefab) {
            meshPrefab = null;
            var list = Entries;
            for (var i = 0; i < list.Length; i++) {
                if (list[i].Kind != kind) {
                    continue;
                }

                meshPrefab = list[i].MeshPrefab;
                return meshPrefab != null;
            }

            return false;
        }

        public bool TryValidate(out string errorMessage) {
            var list = Entries;
            if (list.Length == 0) {
                errorMessage = "DiceMeshCatalog: At least one entry is required.";
                return false;
            }

            for (var i = 0; i < list.Length; i++) {
                if (list[i].MeshPrefab == null) {
                    errorMessage = $"DiceMeshCatalog: Entry[{i}] ({list[i].Kind}) MeshPrefab is required.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
