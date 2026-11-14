using System;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct AttackDefaultPresetEntry
    {
        public PlayerAttackSettings Attack;
        public PlayerAttackPresentationSettings Presentation;
    }

    [CreateAssetMenu(fileName = "AttackDefaultPresetCatalog", menuName = "Dice/Match Setup/Attack Default Preset Catalog")]
    public sealed class AttackDefaultPresetCatalog : ScriptableObject
    {
        [SerializeField] AttackDefaultPresetEntry[] entries = Array.Empty<AttackDefaultPresetEntry>();

        public AttackDefaultPresetEntry[] Entries => entries ?? Array.Empty<AttackDefaultPresetEntry>();

        public PlayerAttackSettings[] Presets {
            get {
                var source = Entries;
                var presets = new PlayerAttackSettings[source.Length];
                for (var i = 0; i < source.Length; i++) {
                    presets[i] = source[i].Attack;
                }

                return presets;
            }
        }

        public int Count {
            get {
                var count = 0;
                var source = Entries;
                for (var i = 0; i < source.Length; i++) {
                    if (source[i].Attack != null) {
                        count++;
                    }
                }

                return count;
            }
        }

        public static string GetDisplayName(PlayerAttackSettings settings) {
            return settings != null ? settings.name : string.Empty;
        }

        public bool TryGetByDisplayName(string displayName, out AttackDefaultPresetEntry entry) {
            entry = default;
            if (string.IsNullOrWhiteSpace(displayName)) {
                return false;
            }

            var source = Entries;
            for (var i = 0; i < source.Length; i++) {
                var candidate = source[i];
                if (candidate.Attack == null) {
                    continue;
                }

                if (string.Equals(
                        GetDisplayName(candidate.Attack),
                        displayName,
                        StringComparison.OrdinalIgnoreCase)) {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetByDisplayName(string displayName, out PlayerAttackSettings settings) {
            if (TryGetByDisplayName(displayName, out AttackDefaultPresetEntry entry)) {
                settings = entry.Attack;
                return true;
            }

            settings = null;
            return false;
        }

        public bool ContainsDisplayName(string displayName) {
            return TryGetByDisplayName(displayName, out PlayerAttackSettings _);
        }

        public bool TryGetPresentation(
            PlayerAttackSettings settings,
            out PlayerAttackPresentationSettings presentation) {
            presentation = null;
            if (settings == null) {
                return false;
            }

            var source = Entries;
            for (var i = 0; i < source.Length; i++) {
                if (source[i].Attack != settings) {
                    continue;
                }

                presentation = source[i].Presentation;
                return presentation != null;
            }

            return false;
        }

        public PlayerAttackSettingsData ToData(PlayerAttackSettings settings) {
            TryGetPresentation(settings, out var presentation);
            return PlayerAttackSettingsData.FromTemplate(settings, presentation);
        }
    }
}
