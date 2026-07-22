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
    public sealed class OnlineLobbyUi : MonoBehaviour
    {
        const string LobbyCanvasName = "OnlineLobbyCanvas";

        OnlineSessionController controller;
        MatchSetupPresetRegistry presetRegistry;
        Canvas canvas;
        GameObject mainPanel;
        GameObject localModePanel;
        GameObject onlineModePanel;
        GameObject matchSetupPanel;
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
        MatchSetupPanelUi setupPanelUi;
        GameMode selectedMode;
        bool onlineSharedSetupActive;
        bool onlineSetupIsHost;
        bool applyingRemoteSetup;
        float onlineSetupSyncTimer;
        string lastSyncedSetupJson = string.Empty;

        public void Configure(
            OnlineSessionController sessionController,
            MatchSetupPresetRegistry registry,
            TMP_FontAsset font) {
            if (font == null) {
                Debug.LogError("[OnlineLobbyUi] lobbyUiFont is not assigned on OnlineSessionController.");
                return;
            }

            controller = sessionController;
            presetRegistry = registry;
            LobbyUiFactory.Configure(font);
            EnsureEventSystem();
            BuildUi();
            ShowMainPanel();

            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.StateChanged += RefreshStatus;
            }
        }

        void OnDestroy() {
            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.StateChanged -= RefreshStatus;
            }
        }

        void Update() {
            TickOnlineSetupSync();
        }

        public void ShowMainPanel() {
            onlineSharedSetupActive = false;
            SetPanel(main: true, localMode: false, onlineMode: false, setup: false, host: false, client: false);
            RefreshStatus();
        }

        public void ShowLocalModePanel() {
            onlineSharedSetupActive = false;
            SetPanel(main: false, localMode: true, onlineMode: false, setup: false, host: false, client: false);
            OnlineSessionState.Instance?.SetStatus("Select a mode.");
            RefreshStatus();
        }

        public void ShowOnlineModePanel() {
            onlineSharedSetupActive = false;
            SetPanel(main: false, localMode: false, onlineMode: true, setup: false, host: false, client: false);
            OnlineSessionState.Instance?.SetStatus("Select online mode (Co-op or Versus).");
            RefreshStatus();
        }

        public void ShowMatchSetupPanel(GameMode mode) {
            onlineSharedSetupActive = false;
            selectedMode = mode;
            ConfigureSetupChrome(isOnline: false, isHost: false);
            SetPanel(main: false, localMode: false, onlineMode: false, setup: true, host: false, client: false);
            RebuildMatchSetupPanel(mode, MatchSetupPersistence.LoadOrCreate(mode, GetRegistry()));
            OnlineSessionState.Instance?.SetStatus($"Configure {GameModeDisplayNames.GetDisplayName(mode)} settings.");
            RefreshStatus();
        }

        public void ShowOnlineSharedSetupPanel(MatchSetupSnapshot snapshot, bool isHost) {
            if (snapshot == null) {
                return;
            }

            onlineSetupIsHost = isHost;
            onlineSharedSetupActive = true;
            selectedMode = snapshot.GameMode;
            ConfigureSetupChrome(isOnline: true, isHost: isHost);
            SetPanel(main: false, localMode: false, onlineMode: false, setup: true, host: false, client: false);
            RebuildMatchSetupPanel(snapshot.GameMode, snapshot);
            lastSyncedSetupJson = BuildSetupJson(snapshot);
            onlineSetupSyncTimer = OnlineSessionConstants.OnlineSetupSyncIntervalSeconds;
            OnlineSessionState.Instance?.SetStatus(
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
            onlineSetupSyncTimer = OnlineSessionConstants.OnlineSetupSyncIntervalSeconds;
            applyingRemoteSetup = false;
        }

        public void ShowHostPanel(string lobbyCode) {
            onlineSharedSetupActive = false;
            SetPanel(main: false, localMode: false, onlineMode: false, setup: false, host: true, client: false);
            if (hostCodeText != null) {
                hostCodeText.text = $"Join Code\n{lobbyCode}\n\nWaiting for player...";
            }

            RefreshStatus();
        }

        public void ShowClientWaitingPanel(string lobbyCode) {
            onlineSharedSetupActive = false;
            SetPanel(main: false, localMode: false, onlineMode: false, setup: false, host: false, client: true);
            if (clientStatusText != null) {
                clientStatusText.text = $"Joined: {lobbyCode}\nWaiting for shared settings...";
            }

            RefreshStatus();
        }

        public void Hide() {
            onlineSharedSetupActive = false;
            if (canvas != null) {
                canvas.gameObject.SetActive(false);
            }
        }

        void RefreshStatus() {
            var status = OnlineSessionState.Instance?.StatusMessage ?? string.Empty;
            if (statusText != null) {
                statusText.text = status;
            }
        }

        void SetPanel(bool main, bool localMode, bool onlineMode, bool setup, bool host, bool client) {
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
                Debug.LogError("[OnlineLobbyUi] setupContentRoot is null after UI resolution.");
                OnlineSessionState.Instance?.SetStatus("Failed to initialize settings UI. Stop Play and try again.");
                return;
            }

            if (registry == null) {
                Debug.LogError("[OnlineLobbyUi] presetRegistry is null.");
                OnlineSessionState.Instance?.SetStatus("Setup presets are not configured.");
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
                Debug.LogError($"[OnlineLobbyUi] Failed to build match setup panel: {ex}");
                OnlineSessionState.Instance?.SetStatus("Failed to build settings UI. Check the Console.");
                return;
            }

            if (setupErrorText != null) {
                setupErrorText.text = string.Empty;
            }
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

            onlineSetupSyncTimer = OnlineSessionConstants.OnlineSetupSyncIntervalSeconds;
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
                OnlineSessionState.Instance?.SetStatus("Setup presets are not configured.");
                return;
            }

            ShowMatchSetupPanel(mode);
        }

        void OnOnlineModeSelected(GameMode mode) {
            controller?.CreateHostLobby(mode);
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
                controller?.LeaveSession();
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
                OnlineSessionState.Instance?.SetStatus("Failed to initialize settings UI. Check MatchSetupPresetRegistry.");
                return;
            }

            if (controller == null) {
                return;
            }

            if (!setupPanelUi.TryBuildSnapshot(out var snapshot, out var error)) {
                if (setupErrorText != null) {
                    setupErrorText.text = error ?? "Invalid settings.";
                }

                OnlineSessionState.Instance?.SetStatus(error ?? "Invalid settings.");
                return;
            }

            if (setupErrorText != null) {
                setupErrorText.text = string.Empty;
            }

            var registry = GetRegistry();
            if (!MatchSetupPersistence.TrySave(snapshot, registry, out var saveError)) {
                Debug.LogError($"[OnlineLobbyUi] Failed to save match setup: {saveError}");
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

            var root = LobbyUiFactory.CreatePanel(canvasObject.transform, "Root", new Color(0f, 0f, 0f, 0.65f));
            LobbyUiFactory.StretchFull(root.GetComponent<RectTransform>());

            statusText = LobbyUiFactory.CreateText(root.transform, "Status", string.Empty, 28, TextAnchor.LowerCenter);
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.1f, 0.05f);
            statusRect.anchorMax = new Vector2(0.9f, 0.15f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            BuildMainPanel(root.transform);
            BuildLocalModePanel(root.transform);
            BuildOnlineModePanel(root.transform);
            BuildMatchSetupPanel(root.transform);
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
            setupPanelUi = null;
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
        }

        void BuildMainPanel(Transform root) {
            mainPanel = LobbyUiFactory.CreatePanel(root, "MainPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(mainPanel.GetComponent<RectTransform>(), new Vector2(520f, 420f));
            LobbyUiFactory.CreateText(mainPanel.transform, "Title", "Dice Game", 40, TextAnchor.UpperCenter);
            LobbyUiFactory.CreateButton(mainPanel.transform, "LocalButton", "Local Play", new Vector2(0f, 80f), OnLocalPlayClicked);
            LobbyUiFactory.CreateButton(mainPanel.transform, "HostButton", "Create Room (Host)", new Vector2(0f, 0f), ShowOnlineModePanel);

            joinCodeInput = LobbyUiFactory.CreateInputField(mainPanel.transform, "JoinCodeInput", "Join code", new Vector2(0f, -90f));
            LobbyUiFactory.CreateButton(mainPanel.transform, "JoinButton", "Join by Code", new Vector2(0f, -170f), () => {
                controller?.JoinLobbyByCode(joinCodeInput != null ? joinCodeInput.text : string.Empty);
            });
        }

        void BuildLocalModePanel(Transform root) {
            localModePanel = LobbyUiFactory.CreatePanel(root, "LocalModePanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(localModePanel.GetComponent<RectTransform>(), new Vector2(520f, 420f));
            LobbyUiFactory.CreateText(localModePanel.transform, "Title", "Mode Select", 36, TextAnchor.UpperCenter);
            LobbyUiFactory.CreateButton(localModePanel.transform, "SingleButton", "Single", new Vector2(0f, 70f), () => {
                OnLocalModeSelected(GameMode.Single);
            });
            LobbyUiFactory.CreateButton(localModePanel.transform, "CoopButton", "Co-op", new Vector2(0f, 0f), () => {
                OnLocalModeSelected(GameMode.Coop);
            });
            LobbyUiFactory.CreateButton(localModePanel.transform, "VersusButton", "Versus", new Vector2(0f, -70f), () => {
                OnLocalModeSelected(GameMode.Versus);
            });
            LobbyUiFactory.CreateButton(localModePanel.transform, "LocalModeBackButton", "Back", new Vector2(0f, -170f), ShowMainPanel);
        }

        void BuildOnlineModePanel(Transform root) {
            onlineModePanel = LobbyUiFactory.CreatePanel(root, "OnlineModePanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(onlineModePanel.GetComponent<RectTransform>(), new Vector2(520f, 360f));
            LobbyUiFactory.CreateText(onlineModePanel.transform, "Title", "Online Mode", 36, TextAnchor.UpperCenter);
            LobbyUiFactory.CreateButton(onlineModePanel.transform, "OnlineCoopButton", "Co-op", new Vector2(0f, 40f), () => {
                OnOnlineModeSelected(GameMode.Coop);
            });
            LobbyUiFactory.CreateButton(onlineModePanel.transform, "OnlineVersusButton", "Versus", new Vector2(0f, -40f), () => {
                OnOnlineModeSelected(GameMode.Versus);
            });
            LobbyUiFactory.CreateButton(onlineModePanel.transform, "OnlineModeBackButton", "Back", new Vector2(0f, -140f), ShowMainPanel);
        }

        void BuildMatchSetupPanel(Transform root) {
            matchSetupPanel = LobbyUiFactory.CreatePanel(root, "MatchSetupPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(matchSetupPanel.GetComponent<RectTransform>(), new Vector2(760f, 860f));
            setupTitleText = LobbyUiFactory.CreateText(matchSetupPanel.transform, "Title", "Settings", 36, TextAnchor.UpperCenter);

            var scrollGo = new GameObject("SetupScroll");
            scrollGo.transform.SetParent(matchSetupPanel.transform, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.08f, 0.22f);
            scrollRect.anchorMax = new Vector2(0.92f, 0.82f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewport = LobbyUiFactory.CreatePanel(scrollGo.transform, "Viewport", new Color(0f, 0f, 0f, 0.15f));
            LobbyUiFactory.StretchFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            setupContentRoot = content.transform;
            var contentRect = content.AddComponent<RectTransform>();
            LobbyUiFactory.ConfigureVerticalScrollContent(contentRect);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            setupErrorText = LobbyUiFactory.CreateText(matchSetupPanel.transform, "SetupError", string.Empty, 22, TextAnchor.LowerCenter);
            var errorRect = setupErrorText.GetComponent<RectTransform>();
            errorRect.anchorMin = new Vector2(0.08f, 0.14f);
            errorRect.anchorMax = new Vector2(0.92f, 0.2f);
            errorRect.offsetMin = Vector2.zero;
            errorRect.offsetMax = Vector2.zero;
            setupErrorText.color = new Color(1f, 0.45f, 0.45f, 1f);

            setupPrimaryButton = LobbyUiFactory.CreateButton(
                matchSetupPanel.transform,
                "PlayButton",
                "Play",
                new Vector2(0f, -320f),
                OnMatchSetupPrimaryClicked);
            setupBackButton = LobbyUiFactory.CreateButton(
                matchSetupPanel.transform,
                "SetupBackButton",
                "Back",
                new Vector2(0f, -390f),
                OnMatchSetupBackClicked);
        }

        void BuildHostPanel(Transform root) {
            hostPanel = LobbyUiFactory.CreatePanel(root, "HostPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(hostPanel.GetComponent<RectTransform>(), new Vector2(520f, 300f));
            hostCodeText = LobbyUiFactory.CreateText(hostPanel.transform, "HostCode", "Join Code", 32, TextAnchor.UpperCenter);
            LobbyUiFactory.CreateButton(hostPanel.transform, "HostLeaveButton", "Cancel", new Vector2(0f, -100f), () => {
                controller?.LeaveSession();
            });
        }

        void BuildClientPanel(Transform root) {
            clientPanel = LobbyUiFactory.CreatePanel(root, "ClientPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(clientPanel.GetComponent<RectTransform>(), new Vector2(520f, 300f));
            clientStatusText = LobbyUiFactory.CreateText(clientPanel.transform, "ClientStatus", "Connecting...", 32, TextAnchor.MiddleCenter);
            LobbyUiFactory.CreateButton(clientPanel.transform, "ClientLeaveButton", "Cancel", new Vector2(0f, -100f), () => {
                controller?.LeaveSession();
            });
        }

        static void EnsureEventSystem() {
            if (FindObjectOfType<EventSystem>() != null) {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
