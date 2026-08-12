using System;
using System.Collections.Generic;
using DiceGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    sealed class AttackPresetLibraryUi
    {
        readonly AttackDefaultPresetCatalog defaultCatalog;
        readonly Func<PlayerAttackSettingsPanelUi.Bindings> getActiveBindings;
        readonly Action onLayoutChanged;
        readonly TMP_Dropdown presetDropdown;
        readonly TMP_InputField nameInput;
        readonly Button deleteButton;
        readonly TextMeshProUGUI statusLabel;
        AttackPresetListEntry[] entries = Array.Empty<AttackPresetListEntry>();

        public AttackPresetLibraryUi(
            Transform parent,
            AttackDefaultPresetCatalog catalog,
            Func<PlayerAttackSettingsPanelUi.Bindings> activeBindingsProvider,
            Action layoutChanged) {
            defaultCatalog = catalog;
            getActiveBindings = activeBindingsProvider
                ?? throw new ArgumentNullException(nameof(activeBindingsProvider));
            onLayoutChanged = layoutChanged;

            SessionUiFactory.CreateLayoutLabel(parent, AttackPresetLabels.Section, 20, 28f);
            SessionUiFactory.CreateLayoutLabel(parent, AttackPresetLabels.Note, 14, 48f);
            SessionUiFactory.CreateLayoutLabel(parent, AttackPresetLabels.Select, 18, 24f);
            presetDropdown = SessionUiFactory.CreateLayoutDropdown(
                parent,
                AttackPresetLabels.Dropdown,
                new[] { AttackPresetLabels.EmptyOption },
                40f);
            presetDropdown.onValueChanged.AddListener(_ => RefreshDeleteInteractable());

            SessionUiFactory.CreateLayoutButton(parent, "LoadAttackPresetButton", AttackPresetLabels.Load, 40f, LoadSelected);
            deleteButton = SessionUiFactory.CreateLayoutButton(
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

        public void RefreshPresetList(string selectUserName = null) {
            entries = BuildEntries();
            var options = new List<string>(entries.Length + 1) { AttackPresetLabels.EmptyOption };
            for (var i = 0; i < entries.Length; i++) {
                options.Add(entries[i].DropdownLabel);
            }

            presetDropdown.ClearOptions();
            presetDropdown.AddOptions(options);

            var selectedIndex = 0;
            if (!string.IsNullOrEmpty(selectUserName)) {
                for (var i = 0; i < entries.Length; i++) {
                    if (entries[i].Kind == AttackPresetKind.User
                        && string.Equals(entries[i].Name, selectUserName, StringComparison.OrdinalIgnoreCase)) {
                        selectedIndex = i + 1;
                        break;
                    }
                }
            }

            presetDropdown.SetValueWithoutNotify(selectedIndex);
            presetDropdown.RefreshShownValue();
            RefreshDeleteInteractable();
            onLayoutChanged?.Invoke();
        }

        AttackPresetListEntry[] BuildEntries() {
            var list = new List<AttackPresetListEntry>();
            if (defaultCatalog != null) {
                var defaults = defaultCatalog.Presets;
                for (var i = 0; i < defaults.Length; i++) {
                    var preset = defaults[i];
                    if (preset == null) {
                        continue;
                    }

                    var name = AttackDefaultPresetCatalog.GetDisplayName(preset);
                    if (string.IsNullOrWhiteSpace(name)) {
                        continue;
                    }

                    list.Add(new AttackPresetListEntry(AttackPresetKind.Default, name, preset));
                }
            }

            var userNames = AttackPresetPersistence.ListPresetNames();
            for (var i = 0; i < userNames.Length; i++) {
                list.Add(new AttackPresetListEntry(AttackPresetKind.User, userNames[i]));
            }

            return list.ToArray();
        }

        void LoadSelected() {
            if (!TryGetSelectedEntry(out var entry)) {
                SetStatus("Select a preset to load.");
                return;
            }

            var bindings = getActiveBindings();
            if (bindings == null) {
                SetStatus("Attack settings UI is not available.");
                return;
            }

            PlayerAttackSettingsData data;
            if (entry.Kind == AttackPresetKind.Default) {
                if (entry.DefaultSource == null) {
                    SetStatus("Default preset asset is missing.");
                    return;
                }

                data = PlayerAttackSettingsData.FromTemplate(entry.DefaultSource);
                if (!data.TryValidate(out var validateError)) {
                    SetStatus(validateError ?? "Default preset is invalid.");
                    return;
                }
            } else if (!AttackPresetPersistence.TryLoad(entry.Name, out data, out var error)) {
                SetStatus(error ?? "Failed to load preset.");
                return;
            }

            PlayerAttackSettingsPanelUi.Apply(bindings, data);
            if (nameInput != null && entry.Kind == AttackPresetKind.User) {
                nameInput.SetTextWithoutNotify(entry.Name);
            }

            onLayoutChanged?.Invoke();
            SetStatus($"Loaded {(entry.Kind == AttackPresetKind.Default ? "default" : "user")} preset '{entry.Name}'.");
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
            if (!AttackPresetPersistence.TryNormalizeName(name, out var normalized, out var nameError)) {
                SetStatus(nameError ?? "Invalid preset name.");
                return;
            }

            if (IsReservedDefaultName(normalized)) {
                SetStatus($"'{normalized}' is a default preset name and cannot be overwritten.");
                return;
            }

            if (!AttackPresetPersistence.TrySave(normalized, data, out var saveError)) {
                SetStatus(saveError ?? "Failed to save preset.");
                return;
            }

            RefreshPresetList(normalized);
            if (nameInput != null) {
                nameInput.SetTextWithoutNotify(normalized);
            }

            SetStatus($"Saved user preset '{normalized}'.");
        }

        void DeleteSelected() {
            if (!TryGetSelectedEntry(out var entry)) {
                SetStatus("Select a user preset to delete.");
                return;
            }

            if (entry.Kind != AttackPresetKind.User) {
                SetStatus("Default presets cannot be deleted.");
                return;
            }

            if (!AttackPresetPersistence.TryDelete(entry.Name, out var error)) {
                SetStatus(error ?? "Failed to delete preset.");
                return;
            }

            RefreshPresetList();
            SetStatus($"Deleted user preset '{entry.Name}'.");
        }

        bool IsReservedDefaultName(string normalizedName) {
            if (defaultCatalog == null) {
                return false;
            }

            var defaults = defaultCatalog.Presets;
            for (var i = 0; i < defaults.Length; i++) {
                var preset = defaults[i];
                if (preset == null) {
                    continue;
                }

                if (!AttackPresetPersistence.TryNormalizeName(
                        AttackDefaultPresetCatalog.GetDisplayName(preset),
                        out var defaultNormalized,
                        out _)) {
                    continue;
                }

                if (string.Equals(defaultNormalized, normalizedName, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        bool TryGetSelectedEntry(out AttackPresetListEntry entry) {
            entry = default;
            var index = presetDropdown.value - 1;
            if (index < 0 || index >= entries.Length) {
                return false;
            }

            entry = entries[index];
            return true;
        }

        void RefreshDeleteInteractable() {
            if (deleteButton == null) {
                return;
            }

            deleteButton.interactable =
                TryGetSelectedEntry(out var entry) && entry.Kind == AttackPresetKind.User;
        }

        void SetStatus(string message) {
            if (statusLabel != null) {
                statusLabel.text = message ?? string.Empty;
            }
        }
    }
}
