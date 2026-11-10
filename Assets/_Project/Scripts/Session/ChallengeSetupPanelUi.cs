using System;
using System.Collections.Generic;
using DiceGame.Config;
using TMPro;
using UnityEngine;

namespace DiceGame.Session
{
    /// <summary>
    /// Challenge title setup: player Control (device) and AttackSettings from the default preset catalog.
    /// </summary>
    public sealed class ChallengeSetupPanelUi
    {
        readonly MatchSetupPresetRegistry registry;
        readonly ChallengeModeSettings challengeSettings;
        readonly AttackDefaultPresetCatalog catalog;
        readonly List<PlayerAttackSettings> presetOptions = new();
        readonly TMP_Dropdown deviceDropdown;
        readonly TMP_Dropdown gamepadIndexDropdown;
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

            var controlDefaults = registry.GetControlDefaults(PlayerSlot.Player1);

            SessionUiFactory.CreateLayoutLabel(parent, "Control Settings", 22, 28f);
            var controlRow = SessionUiFactory.CreateTwoColumnRow(parent, "ChallengeControlRow");
            deviceDropdown = CreateEnumRow(controlRow, "1P Device", new[] { "Keyboard", "Gamepad" });
            gamepadIndexDropdown = CreateEnumRow(controlRow, "1P Gamepad", new[] { "1", "2" });
            deviceDropdown.value = controlDefaults.InputConfig.DeviceKind == PlayerInputDeviceKind.Gamepad
                ? 1
                : 0;
            gamepadIndexDropdown.value = Mathf.Clamp(controlDefaults.InputConfig.GamepadIndex, 0, 1);
            deviceDropdown.onValueChanged.AddListener(_ => RefreshControlVisibility());
            RefreshControlVisibility();

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

        public void ApplyDefaults(MatchSetupSnapshot snapshot) {
            if (snapshot == null) {
                return;
            }

            ApplyControlDefaults(snapshot.Player1.InputConfig);
            TrySelectAttackMatching(snapshot.Player1.Attack);
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
                ReadPlayer1InputConfig(),
                out snapshot,
                out errorMessage);
        }

        void ApplyControlDefaults(PlayerSlotInputConfig inputConfig) {
            if (deviceDropdown == null || gamepadIndexDropdown == null) {
                return;
            }

            deviceDropdown.SetValueWithoutNotify(
                inputConfig.DeviceKind == PlayerInputDeviceKind.Gamepad ? 1 : 0);
            gamepadIndexDropdown.SetValueWithoutNotify(Mathf.Clamp(inputConfig.GamepadIndex, 0, 1));
            deviceDropdown.RefreshShownValue();
            gamepadIndexDropdown.RefreshShownValue();
            RefreshControlVisibility();
        }

        void TrySelectAttackMatching(PlayerAttackSettingsData attack) {
            if (attackDropdown == null || presetOptions.Count < 1) {
                return;
            }

            for (var i = 0; i < presetOptions.Count; i++) {
                var presetData = PlayerAttackSettingsData.FromTemplate(presetOptions[i]);
                if (!AttackDataEquals(presetData, attack)) {
                    continue;
                }

                attackDropdown.SetValueWithoutNotify(i);
                attackDropdown.RefreshShownValue();
                return;
            }
        }

        PlayerSlotInputConfig ReadPlayer1InputConfig() {
            var deviceKind = deviceDropdown != null && deviceDropdown.value == 1
                ? PlayerInputDeviceKind.Gamepad
                : PlayerInputDeviceKind.Keyboard;
            var gamepadIndex = gamepadIndexDropdown != null
                ? Mathf.Clamp(gamepadIndexDropdown.value, 0, 1)
                : 0;
            return new PlayerSlotInputConfig(deviceKind, gamepadIndex);
        }

        void RefreshControlVisibility() {
            if (gamepadIndexDropdown == null) {
                return;
            }

            var showGamepad = deviceDropdown != null && deviceDropdown.value == 1;
            if (gamepadIndexDropdown.transform.parent != null) {
                gamepadIndexDropdown.transform.parent.gameObject.SetActive(showGamepad);
            }
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

        static TMP_Dropdown CreateEnumRow(Transform parent, string label, string[] options) {
            var cell = SessionUiFactory.CreateFieldCell(parent, label);
            SessionUiFactory.CreateLayoutLabel(cell, label, 18, 24f);
            return SessionUiFactory.CreateLayoutDropdown(cell, $"{label}Dropdown", options, 40f);
        }

        static bool AttackDataEquals(PlayerAttackSettingsData a, PlayerAttackSettingsData b) {
            if (!Mathf.Approximately(a.AttackMultiplier, b.AttackMultiplier)
                || !Mathf.Approximately(a.FaceGain, b.FaceGain)
                || !Mathf.Approximately(a.ChainGain, b.ChainGain)
                || !Mathf.Approximately(a.SizeGain, b.SizeGain)
                || !Mathf.Approximately(a.SnatchMultiplier, b.SnatchMultiplier)
                || !Mathf.Approximately(a.Face2Weight, b.Face2Weight)
                || !Mathf.Approximately(a.Face3Weight, b.Face3Weight)
                || !Mathf.Approximately(a.Face4Weight, b.Face4Weight)
                || !Mathf.Approximately(a.Face5Weight, b.Face5Weight)
                || !Mathf.Approximately(a.Face6Weight, b.Face6Weight)) {
                return false;
            }

            var profilesA = a.FaceSendProfiles ?? Array.Empty<FaceAttackSendProfileData>();
            var profilesB = b.FaceSendProfiles ?? Array.Empty<FaceAttackSendProfileData>();
            if (profilesA.Length != profilesB.Length) {
                return false;
            }

            for (var i = 0; i < profilesA.Length; i++) {
                if (!FaceProfileEquals(profilesA[i], profilesB[i])) {
                    return false;
                }
            }

            return true;
        }

        static bool FaceProfileEquals(FaceAttackSendProfileData a, FaceAttackSendProfileData b) {
            var facesA = a.TriggerFaces ?? Array.Empty<int>();
            var facesB = b.TriggerFaces ?? Array.Empty<int>();
            if (facesA.Length != facesB.Length) {
                return false;
            }

            for (var i = 0; i < facesA.Length; i++) {
                if (facesA[i] != facesB[i]) {
                    return false;
                }
            }

            var kindsA = a.SendableKinds ?? Array.Empty<SendableKindLimitData>();
            var kindsB = b.SendableKinds ?? Array.Empty<SendableKindLimitData>();
            if (kindsA.Length != kindsB.Length) {
                return false;
            }

            for (var i = 0; i < kindsA.Length; i++) {
                if (kindsA[i].Kind != kindsB[i].Kind
                    || kindsA[i].MaxCountPerVolley != kindsB[i].MaxCountPerVolley
                    || !Mathf.Approximately(kindsA[i].MinimumPower, kindsB[i].MinimumPower)
                    || !Mathf.Approximately(kindsA[i].SelectionWeight, kindsB[i].SelectionWeight)) {
                    return false;
                }
            }

            return true;
        }
    }
}
