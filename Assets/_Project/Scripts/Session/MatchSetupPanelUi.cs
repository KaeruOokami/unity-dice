using DiceGame.Config;
using TMPro;
using UnityEngine;

namespace DiceGame.Session
{
    sealed class MatchSetupPanelUi
    {
        const int VersusCategoryShared = 0;
        const int VersusCategoryControl = 1;
        const int VersusCategorySpawn = 2;
        const int VersusCategoryCatalog = 3;
        const int VersusCategoryAttack = 4;
        const int VersusCategoryNaturalSend = 5;

        const int NonVersusCategoryShared = 0;
        const int NonVersusCategoryControl = 1;

        readonly MatchSetupPresetRegistry registry;
        readonly GameMode mode;
        readonly Transform contentRoot;

        GameObject[] categoryRoots;
        GameObject playerSwitcherRoot;
        DiceSpawnSettingsPanelUi.Bindings sharedSpawnUi;
        DiceCatalogPanelUi.Bindings sharedCatalogUi;
        JumboDiceSettingsPanelUi.Bindings jumboUi;
        TMP_InputField versusSharedInitialDiceCount;
        TMP_InputField versusWinsToWin;
        AttackPresetLibraryUi attackPresetLibraryUi;
        PlayerSlotUi player1Ui;
        PlayerSlotUi player2Ui;
        int activePlayerSlotIndex;

        sealed class PlayerSlotUi
        {
            public GameObject ControlRoot;
            public GameObject SpawnRoot;
            public GameObject CatalogRoot;
            public GameObject AttackRoot;
            public GameObject NaturalSendRoot;
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
            SessionUiFactory.CreateLayoutLabel(
                contentRoot,
                $"Mode: {GameModeDisplayNames.GetDisplayName(mode)}",
                22,
                30f);
            var defaults = registry.CreateDefaultSnapshot(mode);
            var categoryLabels = MatchSetupCategoryLabels.GetCategoryLabels(mode);
            CreateCategorySwitcher(categoryLabels);
            if (mode != GameMode.Single) {
                CreatePlayerSlotSwitcher();
            }

            categoryRoots = new GameObject[categoryLabels.Length];
            for (var i = 0; i < categoryLabels.Length; i++) {
                var root = SessionUiFactory.CreateVerticalSection(contentRoot, $"Category_{categoryLabels[i]}");
                categoryRoots[i] = root.gameObject;
            }

            if (mode == GameMode.Versus) {
                versusSharedInitialDiceCount = SessionUiFactory.CreateLabeledIntInput(
                    categoryRoots[VersusCategoryShared].transform,
                    "Initial Dice Count (1P/2P Shared)");
                versusWinsToWin = SessionUiFactory.CreateLabeledIntInput(
                    categoryRoots[VersusCategoryShared].transform,
                    "Wins To Win");
                jumboUi = JumboDiceSettingsPanelUi.Build(
                    categoryRoots[VersusCategoryShared].transform,
                    "Jumbo Dice Settings");
                attackPresetLibraryUi = new AttackPresetLibraryUi(
                    categoryRoots[VersusCategoryAttack].transform,
                    registry.AttackDefaultPresetCatalog,
                    GetActiveAttackBindings,
                    RebuildContentLayout);
                player1Ui = CreatePlayerPanels("1P", true, defaults.Player1);
                player2Ui = CreatePlayerPanels("2P", true, defaults.Player2);
                ShowPlayerSlot(0);
            } else {
                sharedSpawnUi = DiceSpawnSettingsPanelUi.Build(
                    categoryRoots[NonVersusCategoryShared].transform,
                    "Shared Dice Spawn Settings");
                sharedCatalogUi = DiceCatalogPanelUi.Build(
                    categoryRoots[NonVersusCategoryShared].transform,
                    "Shared Dice Catalog",
                    defaults.SharedCatalog);
                if (mode == GameMode.Coop) {
                    player1Ui = CreatePlayerPanels("1P", false, defaults.Player1);
                    player2Ui = CreatePlayerPanels("2P", false, defaults.Player2);
                    ShowPlayerSlot(0);
                } else {
                    player1Ui = CreatePlayerPanels("1P", false, defaults.Player1);
                }
            }

            ShowCategory(0);
        }

        void CreateCategorySwitcher(string[] categoryLabels) {
            SessionUiFactory.CreateLayoutLabel(
                contentRoot,
                MatchSetupCategoryLabels.Category,
                18,
                24f);
            var dropdown = SessionUiFactory.CreateLayoutDropdown(
                contentRoot,
                MatchSetupCategoryLabels.CategoryDropdown,
                categoryLabels,
                40f);
            dropdown.onValueChanged.AddListener(ShowCategory);
        }

        void CreatePlayerSlotSwitcher() {
            playerSwitcherRoot = SessionUiFactory.CreateVerticalSection(
                contentRoot,
                "PlayerSwitcher").gameObject;
            SessionUiFactory.CreateLayoutLabel(
                playerSwitcherRoot.transform,
                MatchSetupCategoryLabels.Player,
                18,
                24f);
            var dropdown = SessionUiFactory.CreateLayoutDropdown(
                playerSwitcherRoot.transform,
                MatchSetupCategoryLabels.PlayerSlotDropdown,
                new[] { "1P", "2P" },
                40f);
            dropdown.onValueChanged.AddListener(ShowPlayerSlot);
        }

        void ShowCategory(int index) {
            if (categoryRoots == null) {
                return;
            }

            for (var i = 0; i < categoryRoots.Length; i++) {
                if (categoryRoots[i] != null) {
                    categoryRoots[i].SetActive(i == index);
                }
            }

            if (playerSwitcherRoot != null) {
                playerSwitcherRoot.SetActive(CategoryNeedsPlayerSwitcher(index));
            }

            RebuildContentLayout();
        }

        bool CategoryNeedsPlayerSwitcher(int categoryIndex) {
            if (mode == GameMode.Single || playerSwitcherRoot == null) {
                return false;
            }

            if (mode == GameMode.Coop) {
                return categoryIndex == NonVersusCategoryControl;
            }

            return categoryIndex != VersusCategoryShared;
        }

        void ShowPlayerSlot(int index) {
            activePlayerSlotIndex = index;
            SetPlayerPanelsActive(player1Ui, index == 0);
            SetPlayerPanelsActive(player2Ui, index == 1);
            RebuildContentLayout();
        }

        PlayerAttackSettingsPanelUi.Bindings GetActiveAttackBindings() {
            var section = activePlayerSlotIndex == 0 ? player1Ui : player2Ui;
            return section?.AttackUi;
        }

        static void SetPlayerPanelsActive(PlayerSlotUi section, bool active) {
            if (section == null) {
                return;
            }

            SetActiveIfPresent(section.ControlRoot, active);
            SetActiveIfPresent(section.SpawnRoot, active);
            SetActiveIfPresent(section.CatalogRoot, active);
            SetActiveIfPresent(section.AttackRoot, active);
            SetActiveIfPresent(section.NaturalSendRoot, active);
        }

        static void SetActiveIfPresent(GameObject root, bool active) {
            if (root != null) {
                root.SetActive(active);
            }
        }

        PlayerSlotUi CreatePlayerPanels(string slotLabel, bool versus, PlayerSlotSetup defaults) {
            var controlParent = categoryRoots[
                versus ? VersusCategoryControl : NonVersusCategoryControl].transform;
            var controlRoot = SessionUiFactory.CreateVerticalSection(controlParent, $"{slotLabel}Control");
            SessionUiFactory.CreateLayoutLabel(controlRoot, $"{slotLabel} Settings", 22, 30f);
            var section = new PlayerSlotUi {
                ControlRoot = controlRoot.gameObject,
                AiDropdown = CreateEnumRow(controlRoot, $"{slotLabel} Control", new[] { "Controller", "AI" }),
                DeviceDropdown = CreateEnumRow(controlRoot, $"{slotLabel} Device", new[] { "Keyboard", "Gamepad" }),
                GamepadIndexDropdown = CreateEnumRow(controlRoot, $"{slotLabel} Gamepad", new[] { "1", "2" })
            };

            if (versus) {
                var spawnRoot = SessionUiFactory.CreateVerticalSection(
                    categoryRoots[VersusCategorySpawn].transform,
                    $"{slotLabel}Spawn");
                section.SpawnRoot = spawnRoot.gameObject;
                section.SpawnUi = DiceSpawnSettingsPanelUi.Build(
                    spawnRoot,
                    $"{slotLabel} Dice Spawn Settings",
                    includeInitialDiceCount: false);

                var catalogRoot = SessionUiFactory.CreateVerticalSection(
                    categoryRoots[VersusCategoryCatalog].transform,
                    $"{slotLabel}Catalog");
                section.CatalogRoot = catalogRoot.gameObject;
                section.CatalogUi = DiceCatalogPanelUi.Build(
                    catalogRoot,
                    $"{slotLabel} Dice Catalog",
                    defaults.Catalog);

                var attackRoot = SessionUiFactory.CreateVerticalSection(
                    categoryRoots[VersusCategoryAttack].transform,
                    $"{slotLabel}Attack");
                section.AttackRoot = attackRoot.gameObject;
                section.AttackUi = PlayerAttackSettingsPanelUi.Build(
                    attackRoot,
                    $"{slotLabel} Attack Settings",
                    defaults.Attack);

                var naturalSendRoot = SessionUiFactory.CreateVerticalSection(
                    categoryRoots[VersusCategoryNaturalSend].transform,
                    $"{slotLabel}NaturalSend");
                section.NaturalSendRoot = naturalSendRoot.gameObject;
                section.NaturalSendUi = PlayerNaturalSendSettingsPanelUi.Build(
                    naturalSendRoot,
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
            RebuildContentLayout();
        }

        void RebuildContentLayout() {
            if (contentRoot is RectTransform contentRect) {
                SessionUiFactory.ForceRebuildLayout(contentRect);
            }
        }

        public void ApplyDefaults(MatchSetupSnapshot snapshot) {
            if (snapshot == null) {
                return;
            }

            if (mode == GameMode.Versus) {
                snapshot.NormalizeVersusSharedInitialDiceCount();
                snapshot.NormalizeWinsToWin();
                if (versusSharedInitialDiceCount != null) {
                    versusSharedInitialDiceCount.SetTextWithoutNotify(
                        snapshot.GetVersusSharedInitialDiceCount().ToString());
                }

                if (versusWinsToWin != null) {
                    versusWinsToWin.SetTextWithoutNotify(snapshot.WinsToWin.ToString());
                }

                JumboDiceSettingsPanelUi.Apply(jumboUi, snapshot.Jumbo);
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

                if (versusWinsToWin == null
                    || !int.TryParse(versusWinsToWin.text, out var winsToWin)
                    || winsToWin < 1) {
                    snapshot = null;
                    errorMessage = "Wins To Win must be an integer >= 1.";
                    return false;
                }

                if (!JumboDiceSettingsPanelUi.TryRead(jumboUi, out var jumbo, out errorMessage)) {
                    snapshot = null;
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

                snapshot.WinsToWin = winsToWin;
                snapshot.Jumbo = jumbo;
                snapshot.Player1 = player1;
                snapshot.Player2 = player2;
                snapshot.NormalizeVersusSharedInitialDiceCount();
                snapshot.NormalizeWinsToWin();
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
            SessionUiFactory.CreateLayoutLabel(parent, label, 18, 24f);
            return SessionUiFactory.CreateLayoutDropdown(parent, $"{label}Dropdown", options, 40f);
        }
    }
}
