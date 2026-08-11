using System;
using System.Collections.Generic;
using DiceGame.Config;
using TMPro;
using UnityEngine;

namespace DiceGame.Session
{
    sealed class AttackPresetLibraryUi
    {
        readonly Func<PlayerAttackSettingsPanelUi.Bindings> getActiveBindings;
        readonly Action onLayoutChanged;
        readonly TMP_Dropdown presetDropdown;
        readonly TMP_InputField nameInput;
        readonly TextMeshProUGUI statusLabel;
        string[] presetNames = Array.Empty<string>();

        public AttackPresetLibraryUi(
            Transform parent,
            Func<PlayerAttackSettingsPanelUi.Bindings> activeBindingsProvider,
            Action layoutChanged) {
            getActiveBindings = activeBindingsProvider
                ?? throw new ArgumentNullException(nameof(activeBindingsProvider));
            onLayoutChanged = layoutChanged;

            SessionUiFactory.CreateLayoutLabel(parent, AttackPresetLabels.Section, 20, 28f);
            SessionUiFactory.CreateLayoutLabel(parent, AttackPresetLabels.Note, 14, 36f);
            SessionUiFactory.CreateLayoutLabel(parent, AttackPresetLabels.Select, 18, 24f);
            presetDropdown = SessionUiFactory.CreateLayoutDropdown(
                parent,
                AttackPresetLabels.Dropdown,
                new[] { AttackPresetLabels.EmptyOption },
                40f);

            SessionUiFactory.CreateLayoutButton(parent, "LoadAttackPresetButton", AttackPresetLabels.Load, 40f, LoadSelected);
            SessionUiFactory.CreateLayoutButton(
                parent,
                "DeleteAttackPresetButton",
                AttackPresetLabels.Delete,
                40f,
                DeleteSelected);

            nameInput = SessionUiFactory.CreateLabeledTextInput(parent, AttackPresetLabels.Name);
            nameInput.characterLimit = SessionConstants.AttackPresetNameMaxLength;
            SessionUiFactory.CreateLayoutButton(parent, "SaveAttackPresetButton", AttackPresetLabels.Save, 40f, SaveCurrent);

            statusLabel = SessionUiFactory.CreateLayoutLabel(parent, string.Empty, 16, 28f);
            statusLabel.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            RefreshPresetList();
        }

        public void RefreshPresetList(string selectName = null) {
            presetNames = AttackPresetPersistence.ListPresetNames();
            var options = new List<string>(presetNames.Length + 1) { AttackPresetLabels.EmptyOption };
            options.AddRange(presetNames);
            presetDropdown.ClearOptions();
            presetDropdown.AddOptions(options);

            var selectedIndex = 0;
            if (!string.IsNullOrEmpty(selectName)) {
                for (var i = 0; i < presetNames.Length; i++) {
                    if (string.Equals(presetNames[i], selectName, StringComparison.OrdinalIgnoreCase)) {
                        selectedIndex = i + 1;
                        break;
                    }
                }
            }

            presetDropdown.SetValueWithoutNotify(selectedIndex);
            presetDropdown.RefreshShownValue();
            onLayoutChanged?.Invoke();
        }

        void LoadSelected() {
            if (!TryGetSelectedPresetName(out var name)) {
                SetStatus("Select a preset to load.");
                return;
            }

            var bindings = getActiveBindings();
            if (bindings == null) {
                SetStatus("Attack settings UI is not available.");
                return;
            }

            if (!AttackPresetPersistence.TryLoad(name, out var data, out var error)) {
                SetStatus(error ?? "Failed to load preset.");
                return;
            }

            PlayerAttackSettingsPanelUi.Apply(bindings, data);
            if (nameInput != null) {
                nameInput.SetTextWithoutNotify(name);
            }

            onLayoutChanged?.Invoke();
            SetStatus($"Loaded preset '{name}'.");
        }

        void SaveCurrent() {
            var bindings = getActiveBindings();
            if (bindings == null) {
                SetStatus("Attack settings UI is not available.");
                return;
            }

            if (!PlayerAttackSettingsPanelUi.TryRead(bindings, out var data, out var readError)) {
                SetStatus(readError ?? "Current attack settings are invalid.");
                return;
            }

            var name = nameInput != null ? nameInput.text : string.Empty;
            if (!AttackPresetPersistence.TrySave(name, data, out var saveError)) {
                SetStatus(saveError ?? "Failed to save preset.");
                return;
            }

            AttackPresetPersistence.TryNormalizeName(name, out var normalized, out _);
            RefreshPresetList(normalized);
            if (nameInput != null) {
                nameInput.SetTextWithoutNotify(normalized);
            }

            SetStatus($"Saved preset '{normalized}'.");
        }

        void DeleteSelected() {
            if (!TryGetSelectedPresetName(out var name)) {
                SetStatus("Select a preset to delete.");
                return;
            }

            if (!AttackPresetPersistence.TryDelete(name, out var error)) {
                SetStatus(error ?? "Failed to delete preset.");
                return;
            }

            RefreshPresetList();
            SetStatus($"Deleted preset '{name}'.");
        }

        bool TryGetSelectedPresetName(out string name) {
            name = null;
            var index = presetDropdown.value - 1;
            if (index < 0 || index >= presetNames.Length) {
                return false;
            }

            name = presetNames[index];
            return true;
        }

        void SetStatus(string message) {
            if (statusLabel != null) {
                statusLabel.text = message ?? string.Empty;
            }
        }
    }
}
