using System;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "AttackDefaultPresetCatalog", menuName = "Dice/Attack Default Preset Catalog")]
    public sealed class AttackDefaultPresetCatalog : ScriptableObject
    {
        [SerializeField] PlayerAttackSettings[] presets = Array.Empty<PlayerAttackSettings>();

        public PlayerAttackSettings[] Presets => presets ?? Array.Empty<PlayerAttackSettings>();

        public int Count {
            get {
                var count = 0;
                var source = Presets;
                for (var i = 0; i < source.Length; i++) {
                    if (source[i] != null) {
                        count++;
                    }
                }

                return count;
            }
        }

        public static string GetDisplayName(PlayerAttackSettings settings) {
            return settings != null ? settings.name : string.Empty;
        }

        public bool TryGetByDisplayName(string displayName, out PlayerAttackSettings settings) {
            settings = null;
            if (string.IsNullOrWhiteSpace(displayName)) {
                return false;
            }

            var source = Presets;
            for (var i = 0; i < source.Length; i++) {
                var preset = source[i];
                if (preset == null) {
                    continue;
                }

                if (string.Equals(GetDisplayName(preset), displayName, StringComparison.OrdinalIgnoreCase)) {
                    settings = preset;
                    return true;
                }
            }

            return false;
        }

        public bool ContainsDisplayName(string displayName) {
            return TryGetByDisplayName(displayName, out _);
        }
    }
}
