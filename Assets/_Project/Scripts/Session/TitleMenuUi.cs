using System;
using DiceGame.Config;
using DiceGame.Session.Network;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DiceGame.Session
{
    public sealed class TitleMenuUi : MonoBehaviour
    {
        const string LobbyCanvasName = "TitleMenuCanvas";

        SessionController controller;
        MatchSetupPresetRegistry presetRegistry;
        Canvas canvas;
        GameObject mainPanel;
        GameObject localModePanel;
        GameObject onlineModePanel;
        GameObject matchSetupPanel;
        GameObject controlsPanel;
        GameObject hostPanel;
        GameObject clientPanel;
        TMP_InputField joinCodeInput;
        TextMeshProUGUI statusText;
        TextMeshProUGUI hostCodeText;
        TextMeshProUGUI clientStatusText;
        TextMeshProUGUI setupErrorText;
        TextMeshProUGUI setupTitleText;
        Button setupPrimaryButton;
        Button setupBackButton;
        Transform setupContentRoot;
        Transform controlsContentRoot;
        MatchSetupPanelUi setupPanelUi;
        ControlsPanelUi controlsPanelUi;
        GameMode selectedMode;
        bool onlineSharedSetupActive;
        bool onlineSetupIsHost;
        bool applyingRemoteSetup;
        float onlineSetupSyncTimer;
        string lastSyncedSetupJson = string.Empty;

        public void Configure(
            SessionController sessionController,
            MatchSetupPresetRegistry registry,
            UiFontSettings fontSettings) {
            if (fontSettings == null || !fontSettings.TryGetPrimaryFont(out var font)) {
                Debug.LogError("[TitleMenuUi] UiFontSettings is not assigned on SessionController.");
                return;
            }

            controller = sessionController;
            presetRegistry = registry;
            SessionUiFactory.Configure(font);
            EnsureEventSystem();
            BuildUi();
            ShowMainPanel();

            if (SessionState.Instance != null) {
                SessionState.Instance.StateChanged += RefreshStatus;
            }
        }

        void OnDestroy() {
            controlsPanelUi?.Dispose();
            controlsPanelUi = null;
            if (SessionState.Instance != null) {
                SessionState.Instance.StateChanged -= RefreshStatus;
            }
        }

        void Update() {
            TickOnlineSetupSync();
        }

        public void ShowMainPanel() {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            SetPanel(
                main: true,
                localMode: false,
                onlineMode: false,
                setup: false,
                host: false,
                client: false,
                controls: false);
            RefreshStatus();
        }

        public void ShowLocalModePanel() {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            SetPanel(
                main: false,
                localMode: true,
                onlineMode: false,
                setup: false,
                host: false,
                client: false,
                controls: false);
            SessionState.Instance?.SetStatus("Select a mode.");
            RefreshStatus();
        }

        public void ShowOnlineModePanel() {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            SetPanel(
                main: false,
                localMode: false,
                onlineMode: true,
                setup: false,
                host: false,
                client: false,
                controls: false);
            SessionState.Instance?.SetStatus("Select online mode (Co-op or Versus).");
            RefreshStatus();
        }

        public void ShowControlsPanel() {
            onlineSharedSetupActive = false;
            SetPanel(
                main: false,
                localMode: false,
                onlineMode: false,
                setup: false,
                host: false,
                client: false,
                controls: true);
            RebuildControlsPanel();
            SessionState.Instance?.SetStatus("Configure 1P / 2P keyboard bindings (local to this machine).");
            RefreshStatus();
        }

        public void ShowMatchSetupPanel(GameMode mode) {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            selectedMode = mode;
            ConfigureSetupChrome(isOnline: false, isHost: false);
            SetPanel(
                main: false,
                localMode: false,
                onlineMode: false,
                setup: true,
                host: false,
                client: false,
                controls: false);
            RebuildMatchSetupPanel(mode, MatchSetupPersistence.LoadOrCreate(mode, GetRegistry()));
            SessionState.Instance?.SetStatus($"Configure {GameModeDisplayNames.GetDisplayName(mode)} settings.");
            RefreshStatus();
        }

        public void ShowOnlineSharedSetupPanel(MatchSetupSnapshot snapshot, bool isHost) {
            if (snapshot == null) {
                return;
            }

            controlsPanelUi?.CancelRebind();
            onlineSetupIsHost = isHost;
            onlineSharedSetupActive = true;
            selectedMode = snapshot.GameMode;
            ConfigureSetupChrome(isOnline: true, isHost: isHost);
            SetPanel(
                main: false,
                localMode: false,
                onlineMode: false,
                setup: true,
                host: false,
                client: false,
                controls: false);
            RebuildMatchSetupPanel(snapshot.GameMode, snapshot);
            lastSyncedSetupJson = BuildSetupJson(snapshot);
            onlineSetupSyncTimer = SessionConstants.OnlineSetupSyncIntervalSeconds;
            SessionState.Instance?.SetStatus(
                isHost
                    ? "Shared settings (Host = 1P). Edit anytime, then Start Match."
                    : "Shared settings (Client = 2P). Edits sync to host.");
            RefreshStatus();
        }

        public void ApplyOnlineSetupFromRemote(MatchSetupSnapshot snapshot) {
            if (!onlineSharedSetupActive || snapshot == null || setupPanelUi == null) {
                return;
            }

            applyingRemoteSetup = true;
            setupPanelUi.ApplyDefaults(snapshot);
            lastSyncedSetupJson = BuildSetupJson(snapshot);
            onlineSetupSyncTimer = SessionConstants.OnlineSetupSyncIntervalSeconds;
            applyingRemoteSetup = false;
        }

        public void ShowHostPanel(string lobbyCode) {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            SetPanel(
                main: false,
                localMode: false,
                onlineMode: false,
                setup: false,
                host: true,
                client: false,
                controls: false);
            if (hostCodeText != null) {
                hostCodeText.text = $"Join Code\n{lobbyCode}\n\nWaiting for player...";
            }

            RefreshStatus();
        }

        public void ShowClientWaitingPanel(string lobbyCode) {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            SetPanel(
                main: false,
                localMode: false,
                onlineMode: false,
                setup: false,
                host: false,
                client: true,
                controls: false);
            if (clientStatusText != null) {
                clientStatusText.text = $"Joined: {lobbyCode}\nWaiting for shared settings...";
            }

            RefreshStatus();
        }

        public void ShowClientWaitingForHostMode(string lobbyCode) {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            SetPanel(
                main: false,
                localMode: false,
                onlineMode: false,
                setup: false,
                host: false,
                client: true,
                controls: false);
            if (clientStatusText != null) {
                clientStatusText.text = $"Joined: {lobbyCode}\nWaiting for host to select mode...";
            }

            RefreshStatus();
        }

        public void Hide() {
            onlineSharedSetupActive = false;
            controlsPanelUi?.CancelRebind();
            if (canvas != null) {
                canvas.gameObject.SetActive(false);
            }
        }

        void RefreshStatus() {
            var status = SessionState.Instance?.StatusMessage ?? string.Empty;
            if (statusText != null) {
                statusText.text = status;
            }
        }

        void SetPanel(
            bool main,
            bool localMode,
            bool onlineMode,
            bool setup,
            bool host,
            bool client,
            bool controls) {
            if (canvas != null) {
                canvas.gameObject.SetActive(true);
            }

            if (mainPanel != null) {
                mainPanel.SetActive(main);
            }

            if (localModePanel != null) {
                localModePanel.SetActive(localMode);
            }

            if (onlineModePanel != null) {
                onlineModePanel.SetActive(onlineMode);
            }

            if (matchSetupPanel != null) {
                matchSetupPanel.SetActive(setup);
            }

            if (controlsPanel != null) {
                controlsPanel.SetActive(controls);
            }

            if (hostPanel != null) {
                hostPanel.SetActive(host);
            }

            if (clientPanel != null) {
                clientPanel.SetActive(client);
            }
        }

        void RebuildMatchSetupPanel(GameMode mode, MatchSetupSnapshot defaults) {
            TryResolveUiReferences();

            var registry = GetRegistry();
            if (setupContentRoot == null) {
                Debug.LogError("[TitleMenuUi] setupContentRoot is null after UI resolution.");
                SessionState.Instance?.SetStatus("Failed to initialize settings UI. Stop Play and try again.");
                return;
            }

            if (registry == null) {
                Debug.LogError("[TitleMenuUi] presetRegistry is null.");
                SessionState.Instance?.SetStatus("Setup presets are not configured.");
                return;
            }

            for (var i = setupContentRoot.childCount - 1; i >= 0; i--) {
                Destroy(setupContentRoot.GetChild(i).gameObject);
            }

            try {
                setupPanelUi = new MatchSetupPanelUi(registry, mode, setupContentRoot);
                setupPanelUi.ApplyDefaults(defaults);
            } catch (Exception ex) {
                setupPanelUi = null;
                Debug.LogError($"[TitleMenuUi] Failed to build match setup panel: {ex}");
                SessionState.Instance?.SetStatus("Failed to build settings UI. Check the Console.");
                return;
            }

            if (setupErrorText != null) {
                setupErrorText.text = string.Empty;
            }
        }

        void RebuildControlsPanel() {
            TryResolveUiReferences();
            if (controlsContentRoot == null) {
                Debug.LogError("[TitleMenuUi] controlsContentRoot is null after UI resolution.");
                SessionState.Instance?.SetStatus("Failed to initialize controls UI. Stop Play and try again.");
                return;
            }

            var inputSettings = GetInputSettings();
            if (inputSettings == null) {
                Debug.LogError("[TitleMenuUi] PlayerInputSettings is not available.");
                SessionState.Instance?.SetStatus("Input settings are not configured.");
                return;
            }

            controlsPanelUi?.Dispose();
            controlsPanelUi = null;
            for (var i = controlsContentRoot.childCount - 1; i >= 0; i--) {
                Destroy(controlsContentRoot.GetChild(i).gameObject);
            }

            try {
                PlayerBindingOverridesService.ApplyFromDisk(inputSettings, GetBootstrapInputSettings());
                controlsPanelUi = new ControlsPanelUi(
                    inputSettings,
                    controlsContentRoot,
                    message => SessionState.Instance?.SetStatus(message));
            } catch (Exception ex) {
                controlsPanelUi = null;
                Debug.LogError($"[TitleMenuUi] Failed to build controls panel: {ex}");
                SessionState.Instance?.SetStatus("Failed to build controls UI. Check the Console.");
            }
        }

        PlayerInputSettings GetInputSettings() {
            return GetRegistry()?.DefaultPlayerInputSettings ?? GetBootstrapInputSettings();
        }

        PlayerInputSettings GetBootstrapInputSettings() {
            return controller != null ? controller.PlayerInputSettings : null;
        }

        void ConfigureSetupChrome(bool isOnline, bool isHost) {
            if (setupTitleText != null) {
                setupTitleText.text = isOnline ? "Shared Settings" : "Settings";
            }

            if (setupPrimaryButton != null) {
                setupPrimaryButton.gameObject.SetActive(!isOnline || isHost);
                var label = setupPrimaryButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) {
                    label.text = isOnline ? "Start Match" : "Play";
                }
            }

            if (setupBackButton != null) {
                var label = setupBackButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) {
                    label.text = isOnline ? "Leave" : "Back";
                }
            }
        }

        void TickOnlineSetupSync() {
            if (!onlineSharedSetupActive || applyingRemoteSetup || setupPanelUi == null || controller == null) {
                return;
            }

            if (controller.IsBusy || !controller.IsOnlineSharedSetupReady) {
                return;
            }

            onlineSetupSyncTimer -= Time.unscaledDeltaTime;
            if (onlineSetupSyncTimer > 0f) {
                return;
            }

            onlineSetupSyncTimer = SessionConstants.OnlineSetupSyncIntervalSeconds;
            if (!setupPanelUi.TryBuildSnapshot(out var snapshot, out _)) {
                return;
            }

            var json = BuildSetupJson(snapshot);
            if (json == lastSyncedSetupJson) {
                return;
            }

            if (!controller.TrySubmitOnlineSetupDraft(snapshot, out var error)) {
                if (!string.IsNullOrEmpty(error) && setupErrorText != null) {
                    setupErrorText.text = error;
                }

                return;
            }

            lastSyncedSetupJson = json;
            if (setupErrorText != null) {
                setupErrorText.text = string.Empty;
            }
        }

        void OnLocalPlayClicked() {
            ShowLocalModePanel();
        }

        void OnLocalModeSelected(GameMode mode) {
            if (GetRegistry() == null) {
                SessionState.Instance?.SetStatus("Setup presets are not configured.");
                return;
            }

            ShowMatchSetupPanel(mode);
        }

        void OnOnlineModeSelected(GameMode mode) {
            controller?.ConfirmHostGameMode(mode);
        }

        void OnCreateHostClicked() {
            controller?.CreateHostLobby();
        }

        void OnOnlineModeBackClicked() {
            if (SessionState.Instance != null && SessionState.Instance.IsHost) {
                controller?.ReturnToTitle();
                return;
            }

            ShowMainPanel();
        }

        void OnMatchSetupPrimaryClicked() {
            if (onlineSharedSetupActive) {
                if (!onlineSetupIsHost) {
                    return;
                }

                FlushOnlineSetupBeforeStart();
                controller?.StartOnlineMatchAsHost();
                return;
            }

            OnLocalMatchSetupPlayClicked();
        }

        void OnMatchSetupBackClicked() {
            if (onlineSharedSetupActive) {
                onlineSharedSetupActive = false;
                controller?.ReturnToTitle();
                return;
            }

            ShowLocalModePanel();
        }

        void FlushOnlineSetupBeforeStart() {
            if (setupPanelUi == null || controller == null) {
                return;
            }

            if (!setupPanelUi.TryBuildSnapshot(out var snapshot, out var error)) {
                if (setupErrorText != null) {
                    setupErrorText.text = error ?? "Invalid settings.";
                }

                return;
            }

            controller.TrySubmitOnlineSetupDraft(snapshot, out _);
            lastSyncedSetupJson = BuildSetupJson(snapshot);
        }

        void OnLocalMatchSetupPlayClicked() {
            if (setupPanelUi == null) {
                SessionState.Instance?.SetStatus("Failed to initialize settings UI. Check MatchSetupPresetRegistry.");
                return;
            }

            if (controller == null) {
                return;
            }

            if (!setupPanelUi.TryBuildSnapshot(out var snapshot, out var error)) {
                if (setupErrorText != null) {
                    setupErrorText.text = error ?? "Invalid settings.";
                }

                SessionState.Instance?.SetStatus(error ?? "Invalid settings.");
                return;
            }

            if (setupErrorText != null) {
                setupErrorText.text = string.Empty;
            }

            var registry = GetRegistry();
            if (!MatchSetupPersistence.TrySave(snapshot, registry, out var saveError)) {
                Debug.LogError($"[TitleMenuUi] Failed to save match setup: {saveError}");
            }

            controller.StartLocalPlay(snapshot);
        }

        MatchSetupPresetRegistry GetRegistry() {
            return presetRegistry ?? controller?.MatchSetupPresetRegistry;
        }

        string BuildSetupJson(MatchSetupSnapshot snapshot) {
            var registry = GetRegistry();
            if (snapshot == null || registry == null) {
                return string.Empty;
            }

            var payload = MatchSetupNetworkCodec.ToPayload(snapshot, registry);
            var file = MatchSetupPersistMapper.FromNetworkPayload(payload);
            return JsonUtility.ToJson(file);
        }

        void BuildUi() {
            if (IsUiBuilt()) {
                return;
            }

            DestroyStaleLobbyCanvases();
            ResetUiReferences();

            var canvasObject = new GameObject(LobbyCanvasName);
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var root = SessionUiFactory.CreatePanel(canvasObject.transform, "Root", new Color(0f, 0f, 0f, 0.65f));
            SessionUiFactory.StretchFull(root.GetComponent<RectTransform>());

            statusText = SessionUiFactory.CreateText(root.transform, "Status", string.Empty, 28, TextAnchor.LowerCenter);
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.1f, 0.05f);
            statusRect.anchorMax = new Vector2(0.9f, 0.15f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            BuildMainPanel(root.transform);
            BuildLocalModePanel(root.transform);
            BuildOnlineModePanel(root.transform);
            BuildMatchSetupPanel(root.transform);
            BuildControlsPanel(root.transform);
            BuildHostPanel(root.transform);
            BuildClientPanel(root.transform);
        }

        bool IsUiBuilt() {
            return canvas != null && setupContentRoot != null && matchSetupPanel != null && onlineModePanel != null;
        }

        void DestroyStaleLobbyCanvases() {
            for (var i = transform.childCount - 1; i >= 0; i--) {
                var child = transform.GetChild(i);
                if (child.name == LobbyCanvasName) {
                    Destroy(child.gameObject);
                }
            }
        }

        void ResetUiReferences() {
            canvas = null;
            mainPanel = null;
            localModePanel = null;
            onlineModePanel = null;
            matchSetupPanel = null;
            controlsPanel = null;
            hostPanel = null;
            clientPanel = null;
            joinCodeInput = null;
            statusText = null;
            hostCodeText = null;
            clientStatusText = null;
            setupErrorText = null;
            setupTitleText = null;
            setupPrimaryButton = null;
            setupBackButton = null;
            setupContentRoot = null;
            controlsContentRoot = null;
            setupPanelUi = null;
            controlsPanelUi?.Dispose();
            controlsPanelUi = null;
            onlineSharedSetupActive = false;
            lastSyncedSetupJson = string.Empty;
        }

        void TryResolveUiReferences() {
            if (canvas == null) {
                var canvasTransform = transform.Find(LobbyCanvasName);
                if (canvasTransform != null) {
                    canvas = canvasTransform.GetComponent<Canvas>();
                }
            }

            if (canvas == null) {
                return;
            }

            if (matchSetupPanel == null) {
                var panelTransform = canvas.transform.Find("Root/MatchSetupPanel");
                if (panelTransform != null) {
                    matchSetupPanel = panelTransform.gameObject;
                }
            }

            if (setupContentRoot == null && matchSetupPanel != null) {
                var contentTransform = matchSetupPanel.transform.Find("SetupScroll/Viewport/Content");
                if (contentTransform != null) {
                    setupContentRoot = contentTransform;
                }
            }

            if (controlsPanel == null) {
                var panelTransform = canvas.transform.Find("Root/ControlsPanel");
                if (panelTransform != null) {
                    controlsPanel = panelTransform.gameObject;
                }
            }

            if (controlsContentRoot == null && controlsPanel != null) {
                var contentTransform = controlsPanel.transform.Find("ControlsScroll/Viewport/Content");
                if (contentTransform != null) {
                    controlsContentRoot = contentTransform;
                }
            }
        }

        void BuildMainPanel(Transform root) {
            mainPanel = SessionUiFactory.CreatePanel(root, "MainPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            SessionUiFactory.CenterPanel(mainPanel.GetComponent<RectTransform>(), new Vector2(520f, 520f));
            SessionUiFactory.CreateText(mainPanel.transform, "Title", "Dice Game", 40, TextAnchor.UpperCenter);
            SessionUiFactory.CreateButton(
                mainPanel.transform,
                "LocalButton",
                "Local Play",
                new Vector2(0f, 120f),
                OnLocalPlayClicked);
            SessionUiFactory.CreateButton(
                mainPanel.transform,
                "ControlsButton",
                ControlsPanelLabels.Title,
                new Vector2(0f, 50f),
                ShowControlsPanel);
            SessionUiFactory.CreateButton(
                mainPanel.transform,
                "HostButton",
                "Create Room (Host)",
                new Vector2(0f, -20f),
                OnCreateHostClicked);

            joinCodeInput = SessionUiFactory.CreateInputField(
                mainPanel.transform,
                "JoinCodeInput",
                "Join code",
                new Vector2(0f, -110f));
            BindJoinCodeUppercase(joinCodeInput);
            SessionUiFactory.CreateButton(mainPanel.transform, "JoinButton", "Join by Code", new Vector2(0f, -190f), () => {
                controller?.JoinLobbyByCode(joinCodeInput != null ? joinCodeInput.text : string.Empty);
            });
        }

        void BuildLocalModePanel(Transform root) {
            localModePanel = SessionUiFactory.CreatePanel(root, "LocalModePanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            SessionUiFactory.CenterPanel(localModePanel.GetComponent<RectTransform>(), new Vector2(520f, 420f));
            SessionUiFactory.CreateText(localModePanel.transform, "Title", "Mode Select", 36, TextAnchor.UpperCenter);
            SessionUiFactory.CreateButton(localModePanel.transform, "SingleButton", "Single", new Vector2(0f, 70f), () => {
                OnLocalModeSelected(GameMode.Single);
            });
            SessionUiFactory.CreateButton(localModePanel.transform, "CoopButton", "Co-op", new Vector2(0f, 0f), () => {
                OnLocalModeSelected(GameMode.Coop);
            });
            SessionUiFactory.CreateButton(localModePanel.transform, "VersusButton", "Versus", new Vector2(0f, -70f), () => {
                OnLocalModeSelected(GameMode.Versus);
            });
            SessionUiFactory.CreateButton(localModePanel.transform, "LocalModeBackButton", "Back", new Vector2(0f, -170f), ShowMainPanel);
        }

        void BuildOnlineModePanel(Transform root) {
            onlineModePanel = SessionUiFactory.CreatePanel(root, "OnlineModePanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            SessionUiFactory.CenterPanel(onlineModePanel.GetComponent<RectTransform>(), new Vector2(520f, 360f));
            SessionUiFactory.CreateText(onlineModePanel.transform, "Title", "Online Mode", 36, TextAnchor.UpperCenter);
            SessionUiFactory.CreateButton(onlineModePanel.transform, "OnlineCoopButton", "Co-op", new Vector2(0f, 40f), () => {
                OnOnlineModeSelected(GameMode.Coop);
            });
            SessionUiFactory.CreateButton(onlineModePanel.transform, "OnlineVersusButton", "Versus", new Vector2(0f, -40f), () => {
                OnOnlineModeSelected(GameMode.Versus);
            });
            SessionUiFactory.CreateButton(onlineModePanel.transform, "OnlineModeBackButton", "Leave", new Vector2(0f, -140f), OnOnlineModeBackClicked);
        }

        void BuildMatchSetupPanel(Transform root) {
            matchSetupPanel = SessionUiFactory.CreatePanel(root, "MatchSetupPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            SessionUiFactory.CenterPanel(matchSetupPanel.GetComponent<RectTransform>(), new Vector2(760f, 860f));
            setupTitleText = SessionUiFactory.CreateText(matchSetupPanel.transform, "Title", "Settings", 36, TextAnchor.UpperCenter);

            var scrollGo = new GameObject("SetupScroll");
            scrollGo.transform.SetParent(matchSetupPanel.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.08f, 0.22f);
            scrollRect.anchorMax = new Vector2(0.92f, 0.82f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = SessionUiFactory.CreatePanel(scrollGo.transform, "Viewport", new Color(0f, 0f, 0f, 0.15f));
            SessionUiFactory.StretchFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            setupContentRoot = content.transform;
            var contentRect = content.AddComponent<RectTransform>();
            SessionUiFactory.ConfigureVerticalScrollContent(contentRect);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            setupErrorText = SessionUiFactory.CreateText(matchSetupPanel.transform, "SetupError", string.Empty, 22, TextAnchor.LowerCenter);
            var errorRect = setupErrorText.GetComponent<RectTransform>();
            errorRect.anchorMin = new Vector2(0.08f, 0.14f);
            errorRect.anchorMax = new Vector2(0.92f, 0.2f);
            errorRect.offsetMin = Vector2.zero;
            errorRect.offsetMax = Vector2.zero;
            setupErrorText.color = new Color(1f, 0.45f, 0.45f, 1f);

            setupPrimaryButton = SessionUiFactory.CreateButton(
                matchSetupPanel.transform,
                "PlayButton",
                "Play",
                new Vector2(0f, -320f),
                OnMatchSetupPrimaryClicked);
            setupBackButton = SessionUiFactory.CreateButton(
                matchSetupPanel.transform,
                "SetupBackButton",
                "Back",
                new Vector2(0f, -390f),
                OnMatchSetupBackClicked);
        }

        void BuildControlsPanel(Transform root) {
            controlsPanel = SessionUiFactory.CreatePanel(root, "ControlsPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            SessionUiFactory.CenterPanel(controlsPanel.GetComponent<RectTransform>(), new Vector2(760f, 860f));
            SessionUiFactory.CreateText(
                controlsPanel.transform,
                "Title",
                ControlsPanelLabels.Title,
                36,
                TextAnchor.UpperCenter);

            var scrollGo = new GameObject("ControlsScroll");
            scrollGo.transform.SetParent(controlsPanel.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.08f, 0.18f);
            scrollRect.anchorMax = new Vector2(0.92f, 0.82f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = SessionUiFactory.CreatePanel(scrollGo.transform, "Viewport", new Color(0f, 0f, 0f, 0.15f));
            SessionUiFactory.StretchFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            controlsContentRoot = content.transform;
            var contentRect = content.AddComponent<RectTransform>();
            SessionUiFactory.ConfigureVerticalScrollContent(contentRect);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            SessionUiFactory.CreateButton(
                controlsPanel.transform,
                "ControlsBackButton",
                "Back",
                new Vector2(0f, -360f),
                ShowMainPanel);
        }

        void BuildHostPanel(Transform root) {
            hostPanel = SessionUiFactory.CreatePanel(root, "HostPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            SessionUiFactory.CenterPanel(hostPanel.GetComponent<RectTransform>(), new Vector2(520f, 300f));
            hostCodeText = SessionUiFactory.CreateText(hostPanel.transform, "HostCode", "Join Code", 32, TextAnchor.UpperCenter);
            SessionUiFactory.CreateButton(hostPanel.transform, "HostLeaveButton", "Cancel", new Vector2(0f, -100f), () => {
                controller?.ReturnToTitle();
            });
        }

        void BuildClientPanel(Transform root) {
            clientPanel = SessionUiFactory.CreatePanel(root, "ClientPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            SessionUiFactory.CenterPanel(clientPanel.GetComponent<RectTransform>(), new Vector2(520f, 300f));
            clientStatusText = SessionUiFactory.CreateText(clientPanel.transform, "ClientStatus", "Connecting...", 32, TextAnchor.MiddleCenter);
            SessionUiFactory.CreateButton(clientPanel.transform, "ClientLeaveButton", "Cancel", new Vector2(0f, -100f), () => {
                controller?.ReturnToTitle();
            });
        }

        static void BindJoinCodeUppercase(TMP_InputField input) {
            if (input == null) {
                return;
            }

            input.onValueChanged.AddListener(value => {
                if (string.IsNullOrEmpty(value)) {
                    return;
                }

                var upper = value.ToUpperInvariant();
                if (upper == value) {
                    return;
                }

                var caret = input.stringPosition;
                input.SetTextWithoutNotify(upper);
                input.stringPosition = Mathf.Clamp(caret, 0, upper.Length);
            });
        }

        static void EnsureEventSystem() {
            if (FindFirstObjectByType<EventSystem>() != null) {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
