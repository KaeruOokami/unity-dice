using System;
using DiceGame.Config;
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
        GameObject matchSetupPanel;
        GameObject hostPanel;
        GameObject clientPanel;
        TMP_InputField joinCodeInput;
        TextMeshProUGUI statusText;
        TextMeshProUGUI hostCodeText;
        TextMeshProUGUI clientStatusText;
        TextMeshProUGUI setupErrorText;
        Transform setupContentRoot;
        MatchSetupPanelUi setupPanelUi;
        GameMode selectedMode;

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

        public void ShowMainPanel() {
            SetPanel(main: true, localMode: false, setup: false, host: false, client: false);
            RefreshStatus();
        }

        public void ShowLocalModePanel() {
            SetPanel(main: false, localMode: true, setup: false, host: false, client: false);
            OnlineSessionState.Instance?.SetStatus("Select a mode.");
            RefreshStatus();
        }

        public void ShowMatchSetupPanel(GameMode mode) {
            selectedMode = mode;
            SetPanel(main: false, localMode: false, setup: true, host: false, client: false);
            RebuildMatchSetupPanel(mode);
            OnlineSessionState.Instance?.SetStatus($"Configure {GameModeDisplayNames.GetDisplayName(mode)} settings.");
            RefreshStatus();
        }

        public void ShowHostPanel(string lobbyCode) {
            SetPanel(main: false, localMode: false, setup: false, host: true, client: false);
            if (hostCodeText != null) {
                hostCodeText.text = $"Join Code\n{lobbyCode}";
            }

            RefreshStatus();
        }

        public void ShowClientWaitingPanel(string lobbyCode) {
            SetPanel(main: false, localMode: false, setup: false, host: false, client: true);
            if (clientStatusText != null) {
                clientStatusText.text = $"Joined: {lobbyCode}\nWaiting for host...";
            }

            RefreshStatus();
        }

        public void Hide() {
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

        void SetPanel(bool main, bool localMode, bool setup, bool host, bool client) {
            if (canvas != null) {
                canvas.gameObject.SetActive(true);
            }

            if (mainPanel != null) {
                mainPanel.SetActive(main);
            }

            if (localModePanel != null) {
                localModePanel.SetActive(localMode);
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

        void RebuildMatchSetupPanel(GameMode mode) {
            TryResolveUiReferences();

            var registry = presetRegistry ?? controller?.MatchSetupPresetRegistry;
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
                setupPanelUi.ApplyDefaults(registry.CreateDefaultSnapshot(mode));
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

        void OnLocalPlayClicked() {
            ShowLocalModePanel();
        }

        void OnLocalModeSelected(GameMode mode) {
            var registry = presetRegistry ?? controller?.MatchSetupPresetRegistry;
            if (registry == null) {
                OnlineSessionState.Instance?.SetStatus("Setup presets are not configured.");
                return;
            }

            ShowMatchSetupPanel(mode);
        }

        void OnMatchSetupPlayClicked() {
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

            controller.StartLocalPlay(snapshot);
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
            BuildMatchSetupPanel(root.transform);
            BuildHostPanel(root.transform);
            BuildClientPanel(root.transform);
        }

        bool IsUiBuilt() {
            return canvas != null && setupContentRoot != null && matchSetupPanel != null;
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
            matchSetupPanel = null;
            hostPanel = null;
            clientPanel = null;
            joinCodeInput = null;
            statusText = null;
            hostCodeText = null;
            clientStatusText = null;
            setupErrorText = null;
            setupContentRoot = null;
            setupPanelUi = null;
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
            LobbyUiFactory.CreateButton(mainPanel.transform, "HostButton", "Create Room (Host)", new Vector2(0f, 0f), () => {
                controller?.CreateHostLobby();
            });

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

        void BuildMatchSetupPanel(Transform root) {
            matchSetupPanel = LobbyUiFactory.CreatePanel(root, "MatchSetupPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(matchSetupPanel.GetComponent<RectTransform>(), new Vector2(760f, 860f));
            LobbyUiFactory.CreateText(matchSetupPanel.transform, "Title", "Settings", 36, TextAnchor.UpperCenter);

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

            LobbyUiFactory.CreateButton(matchSetupPanel.transform, "PlayButton", "Play", new Vector2(0f, -320f), OnMatchSetupPlayClicked);
            LobbyUiFactory.CreateButton(matchSetupPanel.transform, "SetupBackButton", "Back", new Vector2(0f, -390f), () => {
                ShowLocalModePanel();
            });
        }

        void BuildHostPanel(Transform root) {
            hostPanel = LobbyUiFactory.CreatePanel(root, "HostPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            LobbyUiFactory.CenterPanel(hostPanel.GetComponent<RectTransform>(), new Vector2(520f, 360f));
            hostCodeText = LobbyUiFactory.CreateText(hostPanel.transform, "HostCode", "Join Code", 36, TextAnchor.UpperCenter);
            LobbyUiFactory.CreateButton(hostPanel.transform, "StartMatchButton", "Start Match", new Vector2(0f, -40f), () => {
                controller?.StartOnlineMatchAsHost();
            });
            LobbyUiFactory.CreateButton(hostPanel.transform, "HostLeaveButton", "Cancel", new Vector2(0f, -130f), () => {
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
