using System;
using System.Collections.Generic;
using DiceGame.Config;
using TMPro;
using UnityEngine;

namespace DiceGame.Session
{
    /// <summary>
    /// Challenge title setup: pick AttackSettings from the default preset catalog only.
    /// </summary>
    public sealed class ChallengeSetupPanelUi
    {
        readonly MatchSetupPresetRegistry registry;
        readonly ChallengeModeSettings challengeSettings;
        readonly AttackDefaultPresetCatalog catalog;
        readonly List<PlayerAttackSettings> presetOptions = new();
        readonly TMP_Dropdown attackDropdown;

        public ChallengeSetupPanelUi(MatchSetupPresetRegistry presetRegistry, Transform parent) {
            registry = presetRegistry
                ?? throw new ArgumentNullException(nameof(presetRegistry));
            challengeSettings = registry.ChallengeModeSettings;
            if (challengeSettings == null) {
                throw new InvalidOperationException(
                    "ChallengeModeSettings is not assigned on MatchSetupPresetRegistry.");
            }

            catalog = challengeSettings.ResolvePlayerAttackCatalog(registry);
            if (catalog == null || catalog.Count < 1) {
                throw new InvalidOperationException(
                    "Challenge requires AttackDefaultPresetCatalog with at least one preset.");
            }

            SessionUiFactory.CreateLayoutLabel(parent, "Attack Settings", 22, 28f);
            BuildPresetOptions();
            var optionLabels = new string[presetOptions.Count];
            for (var i = 0; i < presetOptions.Count; i++) {
                optionLabels[i] = AttackDefaultPresetCatalog.GetDisplayName(presetOptions[i]);
            }

            attackDropdown = SessionUiFactory.CreateLayoutDropdown(
                parent,
                "ChallengeAttackDropdown",
                optionLabels,
                40f);
            attackDropdown.value = 0;
            attackDropdown.RefreshShownValue();

            SessionUiFactory.CreateLayoutLabel(
                parent,
                $"Matches: {challengeSettings.MatchCount} (settings fixed)",
                18,
                28f);
        }

        public bool TryBuildSnapshot(out MatchSetupSnapshot snapshot, out string errorMessage) {
            snapshot = null;
            if (challengeSettings == null) {
                errorMessage = "ChallengeModeSettings is not assigned.";
                return false;
            }

            if (!TryGetSelectedAttack(out var playerAttack, out errorMessage)) {
                return false;
            }

            return challengeSettings.TryCreateSnapshot(
                registry,
                playerAttack,
                matchIndex: 0,
                out snapshot,
                out errorMessage);
        }

        bool TryGetSelectedAttack(out PlayerAttackSettings attack, out string errorMessage) {
            attack = null;
            if (attackDropdown == null || presetOptions.Count < 1) {
                errorMessage = "No attack presets are available.";
                return false;
            }

            var index = Mathf.Clamp(attackDropdown.value, 0, presetOptions.Count - 1);
            attack = presetOptions[index];
            if (attack == null) {
                errorMessage = "Selected attack preset is missing.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        void BuildPresetOptions() {
            presetOptions.Clear();
            var presets = catalog.Presets;
            for (var i = 0; i < presets.Length; i++) {
                if (presets[i] != null) {
                    presetOptions.Add(presets[i]);
                }
            }
        }
    }
}
