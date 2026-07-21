using System;
using System.Threading.Tasks;
using DiceGame.Config;
using DiceGame.Gameplay;
using DiceGame.Session.Network;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session
{
    [DefaultExecutionOrder(-100)]
    public sealed class OnlineSessionController : MonoBehaviour
    {
        [SerializeField] GameBootstrap gameBootstrap;
        [SerializeField] MatchSetupPresetRegistry matchSetupPresetRegistry;
        [SerializeField] TMP_FontAsset lobbyUiFont;
        [SerializeField] bool showLobbyOnStart = true;

        readonly OnlineLobbyFacade lobbyFacade = new();
        OnlineNetMessenger messenger;
        OnlineLobbyUi lobbyUi;
        bool busy;

        public OnlineNetMessenger Messenger => messenger;
        public MatchSetupPresetRegistry MatchSetupPresetRegistry => matchSetupPresetRegistry;

        void Awake() {
            if (OnlineSessionState.Instance == null) {
                gameObject.AddComponent<OnlineSessionState>();
            }

            if (gameBootstrap == null) {
                gameBootstrap = FindObjectOfType<GameBootstrap>();
            }
        }

        void Start() {
            lobbyUi = gameObject.GetComponent<OnlineLobbyUi>();
            if (lobbyUi == null) {
                lobbyUi = gameObject.AddComponent<OnlineLobbyUi>();
            }

            lobbyUi.Configure(this, matchSetupPresetRegistry, lobbyUiFont);

            if (MatchFlowFlags.ConsumeSkipTitle(out var resumePlayMode)) {
                ResumeMatchAfterReload(resumePlayMode);
                return;
            }

            EnterTitlePresentation();

            if (!showLobbyOnStart) {
                var snapshot = matchSetupPresetRegistry != null
                    ? matchSetupPresetRegistry.CreateDefaultSnapshot(GameMode.Single)
                    : null;
                StartLocalPlay(snapshot);
            }
        }

        void Update() {
            if (lobbyFacade.IsHost) {
                _ = lobbyFacade.TickHeartbeatAsync(Time.unscaledDeltaTime);
            }

            RefreshConnectedCount();
        }

        void OnDestroy() {
            messenger?.Dispose();
            messenger = null;
        }

        public bool IsBusy => busy;

        public void StartLocalPlay(MatchSetupSnapshot snapshot) {
            if (busy) {
                return;
            }

            if (snapshot == null) {
                OnlineSessionState.Instance.SetStatus("No local setup selected.");
                return;
            }

            if (matchSetupPresetRegistry == null) {
                OnlineSessionState.Instance.SetStatus("MatchSetupPresetRegistry is not assigned.");
                return;
            }

            if (!snapshot.TryValidate(matchSetupPresetRegistry, out var error)) {
                OnlineSessionState.Instance.SetStatus(error ?? "Invalid settings.");
                return;
            }

            OnlineSessionState.Instance.SetCurrentSetup(snapshot);
            OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Local);
            OnlineSessionState.Instance.SetStatus("Starting local play.");
            OnlineSessionState.Instance.RequestMatchStart();

            if (gameBootstrap != null && gameBootstrap.IsSessionActive) {
                ShowGameplayWorld();
                lobbyUi?.Hide();
                return;
            }

            OnlineSessionState.Instance.ResetMatchFlag();
            OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
            HideGameplayWorld();
            lobbyUi?.ShowMatchSetupPanel(snapshot.GameMode);
            OnlineSessionState.Instance.SetStatus("Failed to start the game. Check the Console.");
        }

        public async void CreateHostLobby() {
            if (busy) {
                return;
            }

            busy = true;
            try {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.OnlineHost);
                OnlineSessionState.Instance.SetStatus("Authenticating...");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                OnlineSessionState.Instance.SetStatus("Reserving Relay...");
                var (allocation, relayJoinCode) = await OnlineRelayFacade.CreateAllocationAsync(
                    OnlineSessionConstants.MaxPlayers - 1);

                OnlineSessionState.Instance.SetStatus("Creating lobby...");
                var lobby = await lobbyFacade.CreateLobbyAsync(relayJoinCode, allocation.Region);
                OnlineSessionState.Instance.SetLobbyCode(lobby.LobbyCode);

                var networkManager = OnlineNetworkHost.EnsureNetworkManager();
                var transport = OnlineRelayFacade.EnsureUnityTransport(networkManager);
                OnlineRelayFacade.ConfigureHostTransport(transport, allocation);

                BindNetworkCallbacks(networkManager);
                if (!networkManager.StartHost()) {
                    throw new InvalidOperationException("Failed to StartHost.");
                }

                EnsureMessenger(networkManager);
                OnlineSessionState.Instance.SetStatus(
                    $"Host ready. Join code: {lobby.LobbyCode}");
                lobbyUi?.ShowHostPanel(lobby.LobbyCode);
            } catch (Exception ex) {
                Debug.LogError($"OnlineSessionController: Host failed: {ex}");
                OnlineSessionState.Instance.SetStatus($"Host failed: {ex.Message}");
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
                await SafeLeaveAsync();
            } finally {
                busy = false;
            }
        }

        public async void JoinLobbyByCode(string lobbyCode) {
            if (busy) {
                return;
            }

            busy = true;
            try {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.OnlineClient);
                OnlineSessionState.Instance.SetStatus("Authenticating...");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                OnlineSessionState.Instance.SetStatus("Joining lobby...");
                var lobby = await lobbyFacade.JoinLobbyByCodeAsync(lobbyCode);
                OnlineSessionState.Instance.SetLobbyCode(lobby.LobbyCode);

                if (!lobbyFacade.TryGetRelayJoinCode(out var relayJoinCode)) {
                    throw new InvalidOperationException("Lobby does not contain Relay join code.");
                }

                OnlineSessionState.Instance.SetStatus("Connecting to Relay...");
                var allocation = await OnlineRelayFacade.JoinAllocationAsync(relayJoinCode);

                var networkManager = OnlineNetworkHost.EnsureNetworkManager();
                var transport = OnlineRelayFacade.EnsureUnityTransport(networkManager);
                OnlineRelayFacade.ConfigureClientTransport(transport, allocation);

                BindNetworkCallbacks(networkManager);
                if (!networkManager.StartClient()) {
                    throw new InvalidOperationException("Failed to StartClient.");
                }

                EnsureMessenger(networkManager);
                BindClientMessengerHandlers();
                OnlineSessionState.Instance.SetStatus("Waiting for host to start...");
                lobbyUi?.ShowClientWaitingPanel(lobby.LobbyCode);
            } catch (Exception ex) {
                Debug.LogError($"OnlineSessionController: Join failed: {ex}");
                OnlineSessionState.Instance.SetStatus($"Join failed: {ex.Message}");
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
                await SafeLeaveAsync();
            } finally {
                busy = false;
            }
        }

        public void StartOnlineMatchAsHost() {
            if (busy) {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) {
                OnlineSessionState.Instance.SetStatus("No host connection.");
                return;
            }

            if (networkManager.ConnectedClientsList.Count < OnlineSessionConstants.MaxPlayers) {
                OnlineSessionState.Instance.SetStatus(
                    $"Waiting for players ({networkManager.ConnectedClientsList.Count}/{OnlineSessionConstants.MaxPlayers})");
                return;
            }

            if (!TryResolveOnlineMatchSetup(out var setup, out var setupError)) {
                OnlineSessionState.Instance.SetStatus(setupError);
                return;
            }

            OnlineSessionState.Instance.SetCurrentSetup(setup);
            var payload = MatchSetupNetworkCodec.ToPayload(setup, matchSetupPresetRegistry);
            messenger?.SendMatchStartToClients(payload);
            OnlineSessionState.Instance.SetStatus("Match starting");
            ShowGameplayWorld();
            OnlineSessionState.Instance.RequestMatchStart();
            lobbyUi?.Hide();
        }

        public async void LeaveSession() {
            if (busy) {
                return;
            }

            busy = true;
            try {
                await SafeLeaveAsync();
                OnlineSessionState.Instance.ResetMatchFlag();
                OnlineSessionState.Instance.ClearCurrentSetup();
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
                OnlineSessionState.Instance.SetLobbyCode(string.Empty);
                OnlineSessionState.Instance.SetStatus("Session ended.");
                EnterTitlePresentation();
                lobbyUi?.ShowMainPanel();
            } finally {
                busy = false;
            }
        }

        public void PrepareReturnToTitle() {
            OnlineSessionState.Instance?.ResetMatchFlag();
            OnlineSessionState.Instance?.ClearCurrentSetup();
            OnlineSessionState.Instance?.SetPlayMode(OnlinePlayMode.Unspecified);
            OnlineSessionState.Instance?.SetLobbyCode(string.Empty);
        }

        void ResumeMatchAfterReload(OnlinePlayMode resumePlayMode) {
            OnlineSessionState.Instance.ResetMatchFlag();
            OnlineSessionState.Instance.SetPlayMode(resumePlayMode);
            var pendingSetup = MatchFlowFlags.ConsumePendingSetup();
            if (pendingSetup != null) {
                OnlineSessionState.Instance.SetCurrentSetup(pendingSetup);
            }
            OnlineSessionState.Instance.SetStatus("Resuming match.");
            ShowGameplayWorld();
            lobbyUi?.Hide();

            if (resumePlayMode == OnlinePlayMode.OnlineHost
                || resumePlayMode == OnlinePlayMode.OnlineClient) {
                var networkManager = NetworkManager.Singleton;
                if (networkManager == null || !networkManager.IsListening) {
                    Debug.LogError("OnlineSessionController: Online match restart requires an active NetworkManager.");
                    EnterTitlePresentation();
                    lobbyUi?.ShowMainPanel();
                    return;
                }

                EnsureMessenger(networkManager);
                if (resumePlayMode == OnlinePlayMode.OnlineClient) {
                    BindClientMessengerHandlers();
                }
            }

            OnlineSessionState.Instance.RequestMatchStart();
        }

        void EnterTitlePresentation() {
            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
                OnlineSessionState.Instance.ResetMatchFlag();
                OnlineSessionState.Instance.ClearCurrentSetup();
                OnlineSessionState.Instance.SetStatus("Choose local play, create a room, or join by code.");
            }

            HideGameplayWorld();
            _ = CleanupNetworkForTitleAsync();
        }

        async Task CleanupNetworkForTitleAsync() {
            UnbindClientMessengerHandlers();
            if (messenger != null) {
                messenger.Dispose();
                messenger = null;
            }

            OnlineNetworkHost.Shutdown();
            await lobbyFacade.LeaveAsync();
        }

        void ShowGameplayWorld() {
            var board = gameBootstrap != null ? gameBootstrap.Board : null;
            GameWorldVisibility.SetBoardVisible(board, true);
        }

        void HideGameplayWorld() {
            var board = gameBootstrap != null ? gameBootstrap.Board : null;
            GameWorldVisibility.SetBoardVisible(board, false);
        }

        void OnMatchStartFromHost() {
            if (OnlineSessionState.Instance.PlayMode != OnlinePlayMode.OnlineClient) {
                return;
            }

            OnlineSessionState.Instance.SetStatus("Match starting");
            ShowGameplayWorld();
            OnlineSessionState.Instance.RequestMatchStart();
            lobbyUi?.Hide();
        }

        void OnMatchSetupFromHost(MatchSetupNetworkPayload payload) {
            if (OnlineSessionState.Instance == null
                || OnlineSessionState.Instance.PlayMode != OnlinePlayMode.OnlineClient) {
                return;
            }

            if (matchSetupPresetRegistry == null) {
                Debug.LogError("OnlineSessionController: MatchSetupPresetRegistry is not assigned.");
                return;
            }

            if (!MatchSetupNetworkCodec.TryFromPayload(
                payload,
                matchSetupPresetRegistry,
                out var snapshot,
                out var error)) {
                Debug.LogError($"OnlineSessionController: Failed to apply host setup: {error}");
                OnlineSessionState.Instance.SetStatus(error ?? "Failed to apply host settings.");
                return;
            }

            OnlineSessionState.Instance.SetCurrentSetup(snapshot);
            OnlineSessionState.Instance.SetStatus("Received host settings.");
        }

        bool TryResolveOnlineMatchSetup(out MatchSetupSnapshot setup, out string errorMessage) {
            setup = OnlineSessionState.Instance?.CurrentSetup?.Clone();
            if (setup == null && matchSetupPresetRegistry != null) {
                setup = matchSetupPresetRegistry.CreateDefaultSnapshot(GameMode.Versus);
            }

            if (setup == null) {
                errorMessage = "MatchSetupPresetRegistry is not assigned.";
                return false;
            }

            NormalizeOnlineHostSetup(setup);

            if (!setup.TryValidate(matchSetupPresetRegistry, out errorMessage)) {
                setup = null;
                return false;
            }

            errorMessage = null;
            return true;
        }

        static void NormalizeOnlineHostSetup(MatchSetupSnapshot setup) {
            var player1 = setup.Player1;
            player1.IsAi = false;
            player1.InputConfig = new PlayerSlotInputConfig(PlayerInputDeviceKind.Keyboard, 0);
            setup.Player1 = player1;

            var player2 = setup.Player2;
            player2.IsAi = false;
            setup.Player2 = player2;
        }

        void EnsureMessenger(NetworkManager networkManager) {
            UnbindClientMessengerHandlers();
            messenger?.Dispose();
            messenger = new OnlineNetMessenger(networkManager);
            messenger.Register();
        }

        void BindClientMessengerHandlers() {
            if (messenger == null) {
                return;
            }

            messenger.MatchSetupReceived -= OnMatchSetupFromHost;
            messenger.MatchStartReceived -= OnMatchStartFromHost;
            messenger.MatchSetupReceived += OnMatchSetupFromHost;
            messenger.MatchStartReceived += OnMatchStartFromHost;
        }

        void UnbindClientMessengerHandlers() {
            if (messenger == null) {
                return;
            }

            messenger.MatchSetupReceived -= OnMatchSetupFromHost;
            messenger.MatchStartReceived -= OnMatchStartFromHost;
        }

        void BindNetworkCallbacks(NetworkManager networkManager) {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        void OnClientConnected(ulong clientId) {
            RefreshConnectedCount();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) {
                OnlineSessionState.Instance.SetStatus(
                    $"Connected {NetworkManager.Singleton.ConnectedClientsList.Count}/{OnlineSessionConstants.MaxPlayers}");
            }
        }

        void OnClientDisconnected(ulong clientId) {
            RefreshConnectedCount();
            if (OnlineSessionState.Instance != null && OnlineSessionState.Instance.IsMatchRunning) {
                OnlineSessionState.Instance.SetStatus("Opponent disconnected.");
            }
        }

        void RefreshConnectedCount() {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) {
                OnlineSessionState.Instance?.SetConnectedPlayerCount(0);
                return;
            }

            OnlineSessionState.Instance?.SetConnectedPlayerCount(
                NetworkManager.Singleton.ConnectedClientsList.Count);
        }

        async Task SafeLeaveAsync() {
            UnbindClientMessengerHandlers();
            if (messenger != null) {
                messenger.Dispose();
                messenger = null;
            }

            OnlineNetworkHost.Shutdown();
            await lobbyFacade.LeaveAsync();
        }

        public GameBootstrap GameBootstrap => gameBootstrap;
    }
}
