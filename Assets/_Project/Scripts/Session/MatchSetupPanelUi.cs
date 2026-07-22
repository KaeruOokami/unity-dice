using DiceGame.Config;
using TMPro;
using UnityEngine;

namespace DiceGame.Session
{
    sealed class MatchSetupPanelUi
    {
        readonly MatchSetupPresetRegistry registry;
        readonly GameMode mode;
        readonly Transform contentRoot;

        DiceSpawnSettingsPanelUi.Bindings sharedSpawnUi;
        DiceCatalogPanelUi.Bindings sharedCatalogUi;
        TMP_InputField versusSharedInitialDiceCount;
        PlayerSlotUi player1Ui;
        PlayerSlotUi player2Ui;

        sealed class PlayerSlotUi
        {
            public GameObject Root;
            public TMP_Dropdown AiDropdown;
            public TMP_Dropdown DeviceDropdown;
            public TMP_Dropdown GamepadIndexDropdown;
            public DiceSpawnSettingsPanelUi.Bindings SpawnUi;
            public DiceCatalogPanelUi.Bindings CatalogUi;
            public PlayerAttackSettingsPanelUi.Bindings AttackUi;
            public PlayerNaturalSendSettingsPanelUi.Bindings NaturalSendUi;
        }

        public MatchSetupPanelUi(
            MatchSetupPresetRegistry presetRegistry,
            GameMode gameMode,
            Transform parent) {
            registry = presetRegistry;
            mode = gameMode;
            contentRoot = parent;
            Build();
        }

        void Build() {
            LobbyUiFactory.CreateLayoutLabel(
                contentRoot,
                $"Mode: {GameModeDisplayNames.GetDisplayName(mode)}",
                22,
                30f);
            var defaults = registry.CreateDefaultSnapshot(mode);

            if (mode == GameMode.Versus) {
                versusSharedInitialDiceCount = LobbyUiFactory.CreateLabeledIntInput(
                    contentRoot,
                    "Initial Dice Count (1P/2P Shared)");
                CreatePlayerSlotSwitcher();
                player1Ui = CreatePlayerSection("1P", true, defaults.Player1);
                player2Ui = CreatePlayerSection("2P", true, defaults.Player2);
                ShowPlayerSlot(0);
            } else {
                sharedSpawnUi = DiceSpawnSettingsPanelUi.Build(contentRoot, "Shared Dice Spawn Settings");
                sharedCatalogUi = DiceCatalogPanelUi.Build(
                    contentRoot,
                    "Shared Dice Catalog",
                    defaults.SharedCatalog);
                if (mode == GameMode.Coop) {
                    CreatePlayerSlotSwitcher();
                    player1Ui = CreatePlayerSection("1P", false, defaults.Player1);
                    player2Ui = CreatePlayerSection("2P", false, defaults.Player2);
                    ShowPlayerSlot(0);
                } else {
                    player1Ui = CreatePlayerSection("1P", false, defaults.Player1);
                }
            }

            if (contentRoot is RectTransform contentRect) {
                LobbyUiFactory.ForceRebuildLayout(contentRect);
            }
        }

        void CreatePlayerSlotSwitcher() {
            LobbyUiFactory.CreateLayoutLabel(contentRoot, "Player", 18, 24f);
            var dropdown = LobbyUiFactory.CreateLayoutDropdown(
                contentRoot,
                "PlayerSlotDropdown",
                new[] { "1P", "2P" },
                40f);
            dropdown.onValueChanged.AddListener(ShowPlayerSlot);
        }

        void ShowPlayerSlot(int index) {
            if (player1Ui?.Root != null) {
                player1Ui.Root.SetActive(index == 0);
            }

            if (player2Ui?.Root != null) {
                player2Ui.Root.SetActive(index == 1);
            }

            if (contentRoot is RectTransform contentRect) {
                LobbyUiFactory.ForceRebuildLayout(contentRect);
            }
        }

        PlayerSlotUi CreatePlayerSection(string slotLabel, bool versus, PlayerSlotSetup defaults) {
            var root = LobbyUiFactory.CreateVerticalSection(contentRoot, $"{slotLabel}Root");
            LobbyUiFactory.CreateLayoutLabel(root, $"{slotLabel} Settings", 22, 30f);
            var section = new PlayerSlotUi {
                Root = root.gameObject,
                AiDropdown = CreateEnumRow(root, $"{slotLabel} Control", new[] { "Controller", "AI" }),
                DeviceDropdown = CreateEnumRow(root, $"{slotLabel} Device", new[] { "Keyboard", "Gamepad" }),
                GamepadIndexDropdown = CreateEnumRow(root, $"{slotLabel} Gamepad", new[] { "1", "2" })
            };

            if (versus) {
                section.SpawnUi = DiceSpawnSettingsPanelUi.Build(
                    root,
                    $"{slotLabel} Dice Spawn Settings",
                    includeInitialDiceCount: false);
                section.CatalogUi = DiceCatalogPanelUi.Build(
                    root,
                    $"{slotLabel} Dice Catalog",
                    defaults.Catalog);
                section.AttackUi = PlayerAttackSettingsPanelUi.Build(
                    root,
                    $"{slotLabel} Attack Settings",
                    defaults.Attack);
                section.NaturalSendUi = PlayerNaturalSendSettingsPanelUi.Build(
                    root,
                    $"{slotLabel} Natural Send Settings",
                    defaults.NaturalSend);
            }

            section.AiDropdown.onValueChanged.AddListener(_ => RefreshPlayerControlVisibility(section));
            section.DeviceDropdown.onValueChanged.AddListener(_ => RefreshPlayerControlVisibility(section));
            RefreshPlayerControlVisibility(section);
            return section;
        }

        void RefreshPlayerControlVisibility(PlayerSlotUi section) {
            var isAi = section.AiDropdown.value == 1;
            section.DeviceDropdown.gameObject.SetActive(!isAi);
            section.GamepadIndexDropdown.gameObject.SetActive(!isAi && section.DeviceDropdown.value == 1);
            if (contentRoot is RectTransform contentRect) {
                LobbyUiFactory.ForceRebuildLayout(contentRect);
            }
        }

        public void ApplyDefaults(MatchSetupSnapshot snapshot) {
            if (snapshot == null) {
                return;
            }

            if (mode == GameMode.Versus) {
                snapshot.NormalizeVersusSharedInitialDiceCount();
                if (versusSharedInitialDiceCount != null) {
                    versusSharedInitialDiceCount.SetTextWithoutNotify(
                        snapshot.GetVersusSharedInitialDiceCount().ToString());
                }
            } else {
                DiceSpawnSettingsPanelUi.Apply(sharedSpawnUi, snapshot.SharedSpawn);
                DiceCatalogPanelUi.Apply(sharedCatalogUi, snapshot.SharedCatalog);
            }

            ApplyPlayerDefaults(player1Ui, snapshot.Player1, mode == GameMode.Versus);
            if (player2Ui != null) {
                ApplyPlayerDefaults(player2Ui, snapshot.Player2, mode == GameMode.Versus);
            }
        }

        void ApplyPlayerDefaults(PlayerSlotUi section, PlayerSlotSetup setup, bool versus) {
            if (section == null) {
                return;
            }

            section.AiDropdown.value = setup.IsAi ? 1 : 0;
            section.DeviceDropdown.value = setup.InputConfig.DeviceKind == PlayerInputDeviceKind.Gamepad ? 1 : 0;
            section.GamepadIndexDropdown.value = Mathf.Clamp(setup.InputConfig.GamepadIndex, 0, 1);

            if (versus) {
                DiceSpawnSettingsPanelUi.Apply(section.SpawnUi, setup.Spawn);
                DiceCatalogPanelUi.Apply(section.CatalogUi, setup.Catalog);
                PlayerAttackSettingsPanelUi.Apply(section.AttackUi, setup.Attack);
                PlayerNaturalSendSettingsPanelUi.Apply(section.NaturalSendUi, setup.NaturalSend);
            }

            RefreshPlayerControlVisibility(section);
        }

        public bool TryBuildSnapshot(out MatchSetupSnapshot snapshot, out string errorMessage) {
            snapshot = new MatchSetupSnapshot {
                GameMode = mode
            };

            if (mode == GameMode.Versus) {
                if (versusSharedInitialDiceCount == null
                    || !int.TryParse(versusSharedInitialDiceCount.text, out var sharedInitialDiceCount)) {
                    snapshot = null;
                    errorMessage = "Initial Dice Count must be an integer.";
                    return false;
                }

                if (!TryBuildPlayerSetup(
                        player1Ui,
                        true,
                        sharedInitialDiceCount,
                        out var player1,
                        out errorMessage)) {
                    snapshot = null;
                    return false;
                }

                if (!TryBuildPlayerSetup(
                        player2Ui,
                        true,
                        sharedInitialDiceCount,
                        out var player2,
                        out errorMessage)) {
                    snapshot = null;
                    return false;
                }

                snapshot.Player1 = player1;
                snapshot.Player2 = player2;
                snapshot.NormalizeVersusSharedInitialDiceCount();
            } else {
                if (!DiceSpawnSettingsPanelUi.TryRead(sharedSpawnUi, out var sharedSpawn, out errorMessage)) {
                    snapshot = null;
                    return false;
                }

                if (!DiceCatalogPanelUi.TryRead(sharedCatalogUi, out var sharedCatalog, out errorMessage)) {
                    snapshot = null;
                    return false;
                }

                snapshot.SharedSpawn = sharedSpawn;
                snapshot.SharedCatalog = sharedCatalog;
                if (!TryBuildPlayerSetup(player1Ui, false, null, out var player1, out errorMessage)) {
                    snapshot = null;
                    return false;
                }

                snapshot.Player1 = player1;
                if (mode == GameMode.Coop) {
                    if (!TryBuildPlayerSetup(player2Ui, false, null, out var player2, out errorMessage)) {
                        snapshot = null;
                        return false;
                    }

                    snapshot.Player2 = player2;
                } else {
                    snapshot.Player2 = default;
                }
            }

            if (!snapshot.TryValidate(registry, out errorMessage)) {
                snapshot = null;
                return false;
            }

            errorMessage = null;
            return true;
        }

        bool TryBuildPlayerSetup(
            PlayerSlotUi section,
            bool versus,
            int? sharedInitialDiceCount,
            out PlayerSlotSetup setup,
            out string errorMessage) {
            setup = default;
            if (section == null) {
                errorMessage = "Player settings UI is not initialized.";
                return false;
            }

            var isAi = section.AiDropdown.value == 1;
            var deviceKind = section.DeviceDropdown.value == 1
                ? PlayerInputDeviceKind.Gamepad
                : PlayerInputDeviceKind.Keyboard;

            DiceSpawnSettingsData spawn = default;
            DiceCatalogData catalog = DiceCatalogData.Empty();
            PlayerAttackSettingsData attack = default;
            PlayerNaturalSendSettingsData naturalSend = PlayerNaturalSendSettingsData.Empty();
            if (versus) {
                if (!DiceSpawnSettingsPanelUi.TryRead(
                        section.SpawnUi,
                        out spawn,
                        out errorMessage,
                        sharedInitialDiceCount)) {
                    return false;
                }

                if (!DiceCatalogPanelUi.TryRead(section.CatalogUi, out catalog, out errorMessage)) {
                    return false;
                }

                if (!PlayerAttackSettingsPanelUi.TryRead(section.AttackUi, out attack, out errorMessage)) {
                    return false;
                }

                if (!PlayerNaturalSendSettingsPanelUi.TryRead(section.NaturalSendUi, out naturalSend, out errorMessage)) {
                    return false;
                }
            }

            setup = PlayerSlotSetup.CreateDefault(
                isAi,
                new PlayerSlotInputConfig(deviceKind, section.GamepadIndexDropdown.value),
                spawn,
                catalog,
                attack,
                naturalSend);
            errorMessage = null;
            return true;
        }

        static TMP_Dropdown CreateEnumRow(Transform parent, string label, string[] options) {
            LobbyUiFactory.CreateLayoutLabel(parent, label, 18, 24f);
            return LobbyUiFactory.CreateLayoutDropdown(parent, $"{label}Dropdown", options, 40f);
        }
    }
}
