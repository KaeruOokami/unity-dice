using System;
using System.Threading.Tasks;
using DiceGame.Config;
using DiceGame.Gameplay;
using DiceGame.Session.Network;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
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
        bool onlineSharedSetupReady;
        float identityHandshakeTimer;

        public OnlineNetMessenger Messenger => messenger;
        public MatchSetupPresetRegistry MatchSetupPresetRegistry => matchSetupPresetRegistry;
        public bool IsOnlineSharedSetupReady => onlineSharedSetupReady;

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
            TickIdentityHandshake();
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

        public async void CreateHostLobby(GameMode mode) {
            if (busy) {
                return;
            }

            if (mode != GameMode.Coop && mode != GameMode.Versus) {
                OnlineSessionState.Instance.SetStatus("Online mode must be Co-op or Versus.");
                return;
            }

            busy = true;
            onlineSharedSetupReady = false;
            try {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.OnlineHost);
                OnlineSessionState.Instance.SetOnlineGameMode(mode);
                OnlineSessionState.Instance.ClearRemotePeerPlayerId();
                OnlineSessionState.Instance.SetStatus("Authenticating...");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                OnlineSessionState.Instance.SetStatus("Reserving Relay...");
                var (allocation, relayJoinCode) = await OnlineRelayFacade.CreateAllocationAsync(
                    OnlineSessionConstants.MaxPlayers - 1);

                OnlineSessionState.Instance.SetStatus("Creating lobby...");
                var lobby = await lobbyFacade.CreateLobbyAsync(relayJoinCode, allocation.Region, mode);
                OnlineSessionState.Instance.SetLobbyCode(lobby.LobbyCode);

                var networkManager = OnlineNetworkHost.EnsureNetworkManager();
                var transport = OnlineRelayFacade.EnsureUnityTransport(networkManager);
                OnlineRelayFacade.ConfigureHostTransport(transport, allocation);

                BindNetworkCallbacks(networkManager);
                if (!networkManager.StartHost()) {
                    throw new InvalidOperationException("Failed to StartHost.");
                }

                EnsureMessenger(networkManager);
                BindHostMessengerHandlers();
                identityHandshakeTimer = OnlineSessionConstants.OnlineIdentityRetryIntervalSeconds;
                OnlineSessionState.Instance.SetStatus(
                    $"Host ready ({GameModeDisplayNames.GetDisplayName(mode)}). Join code: {lobby.LobbyCode}");
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
            onlineSharedSetupReady = false;
            try {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.OnlineClient);
                OnlineSessionState.Instance.ClearRemotePeerPlayerId();
                OnlineSessionState.Instance.SetStatus("Authenticating...");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                OnlineSessionState.Instance.SetStatus("Joining lobby...");
                var lobby = await lobbyFacade.JoinLobbyByCodeAsync(lobbyCode);
                OnlineSessionState.Instance.SetLobbyCode(lobby.LobbyCode);

                if (lobbyFacade.TryGetGameMode(out var mode)) {
                    OnlineSessionState.Instance.SetOnlineGameMode(mode);
                } else {
                    OnlineSessionState.Instance.SetOnlineGameMode(GameMode.Versus);
                }

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
                identityHandshakeTimer = 0f;
                TrySendLocalPlayerIdentity();
                OnlineSessionState.Instance.SetStatus("Waiting for host...");
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

        public bool TrySubmitOnlineSetupDraft(MatchSetupSnapshot snapshot, out string errorMessage) {
            errorMessage = null;
            if (snapshot == null || matchSetupPresetRegistry == null) {
                errorMessage = "Setup is not ready.";
                return false;
            }

            if (!onlineSharedSetupReady) {
                return false;
            }

            snapshot.GameMode = OnlineSessionState.Instance.OnlineGameMode;
            if (!snapshot.TryValidate(matchSetupPresetRegistry, out errorMessage)) {
                return false;
            }

            var payload = MatchSetupNetworkCodec.ToPayload(snapshot, matchSetupPresetRegistry);
            if (OnlineSessionState.Instance.IsHost) {
                ApplyHostDraft(snapshot, broadcast: true);
                return true;
            }

            messenger?.SendMatchSetupUpdateToServer(payload);
            OnlineSessionState.Instance.SetCurrentSetup(snapshot);
            return true;
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

            if (!onlineSharedSetupReady) {
                OnlineSessionState.Instance.SetStatus("Waiting for shared settings...");
                return;
            }

            if (!TryResolveOnlineMatchSetup(out var setup, out var setupError)) {
                OnlineSessionState.Instance.SetStatus(setupError);
                return;
            }

            OnlineSessionState.Instance.SetCurrentSetup(setup);
            var payload = MatchSetupNetworkCodec.ToPayload(setup, matchSetupPresetRegistry);
            payload.MatchSeed = UnityEngine.Random.Range(1, int.MaxValue);
            OnlineSessionState.Instance.SetMatchSeed(payload.MatchSeed);
            messenger?.SendMatchStartToClients(payload);
            onlineSharedSetupReady = false;
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
                onlineSharedSetupReady = false;
                OnlineSessionState.Instance.ResetMatchFlag();
                OnlineSessionState.Instance.ClearCurrentSetup();
                OnlineSessionState.Instance.ClearRemotePeerPlayerId();
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
            onlineSharedSetupReady = false;
            OnlineSessionState.Instance?.ResetMatchFlag();
            OnlineSessionState.Instance?.ClearCurrentSetup();
            OnlineSessionState.Instance?.ClearRemotePeerPlayerId();
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
                if (resumePlayMode == OnlinePlayMode.OnlineHost) {
                    BindHostMessengerHandlers();
                } else {
                    BindClientMessengerHandlers();
                }
            }

            OnlineSessionState.Instance.RequestMatchStart();
        }

        void EnterTitlePresentation() {
            onlineSharedSetupReady = false;
            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
                OnlineSessionState.Instance.ResetMatchFlag();
                OnlineSessionState.Instance.ClearCurrentSetup();
                OnlineSessionState.Instance.ClearRemotePeerPlayerId();
                OnlineSessionState.Instance.SetStatus("Choose local play, create a room, or join by code.");
            }

            HideGameplayWorld();
            _ = CleanupNetworkForTitleAsync();
        }

        async Task CleanupNetworkForTitleAsync() {
            UnbindMessengerHandlers();
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

            onlineSharedSetupReady = false;
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

            if (!TryApplyPayload(payload, out var snapshot, out var error)) {
                Debug.LogError($"OnlineSessionController: Failed to apply host setup: {error}");
                OnlineSessionState.Instance.SetStatus(error ?? "Failed to apply host settings.");
                return;
            }

            OnlineSessionState.Instance.SetCurrentSetup(snapshot);
            OnlineSessionState.Instance.SetOnlineGameMode(snapshot.GameMode);
            if (payload.MatchSeed != 0) {
                OnlineSessionState.Instance.SetMatchSeed(payload.MatchSeed);
            }

            OnlineSessionState.Instance.SetStatus("Received host settings.");
        }

        void OnMatchSetupBroadcast(MatchSetupNetworkPayload payload) {
            if (OnlineSessionState.Instance == null
                || OnlineSessionState.Instance.PlayMode != OnlinePlayMode.OnlineClient
                || OnlineSessionState.Instance.IsMatchRunning) {
                return;
            }

            if (!TryApplyPayload(payload, out var snapshot, out var error)) {
                Debug.LogError($"OnlineSessionController: Failed to apply setup broadcast: {error}");
                OnlineSessionState.Instance.SetStatus(error ?? "Failed to apply shared settings.");
                return;
            }

            OnlineSessionState.Instance.SetCurrentSetup(snapshot);
            OnlineSessionState.Instance.SetOnlineGameMode(snapshot.GameMode);
            if (onlineSharedSetupReady) {
                lobbyUi?.ApplyOnlineSetupFromRemote(snapshot);
            } else {
                onlineSharedSetupReady = true;
                lobbyUi?.ShowOnlineSharedSetupPanel(snapshot, isHost: false);
            }

            OnlineSessionState.Instance.SetStatus("Shared settings ready.");
        }

        void OnMatchSetupUpdateFromClient(ulong senderClientId, MatchSetupNetworkPayload payload) {
            if (!OnlineSessionState.Instance.IsHost || OnlineSessionState.Instance.IsMatchRunning) {
                return;
            }

            if (!TryApplyPayload(payload, out var snapshot, out var error)) {
                Debug.LogError($"OnlineSessionController: Rejected client setup update: {error}");
                return;
            }

            snapshot.GameMode = OnlineSessionState.Instance.OnlineGameMode;
            ApplyHostDraft(snapshot, broadcast: true);
            lobbyUi?.ApplyOnlineSetupFromRemote(snapshot);
        }

        void OnPlayerIdentity(ulong senderClientId, string playerId) {
            if (!OnlineSessionState.Instance.IsHost || OnlineSessionState.Instance.IsMatchRunning) {
                return;
            }

            if (string.IsNullOrWhiteSpace(playerId)) {
                Debug.LogError("OnlineSessionController: Received empty player identity.");
                return;
            }

            Debug.Log(
                $"OnlineSessionController: Received player identity from client {senderClientId}: {playerId}");

            var alreadyReady =
                onlineSharedSetupReady
                && string.Equals(
                    OnlineSessionState.Instance.RemotePeerPlayerId,
                    playerId,
                    StringComparison.Ordinal);

            OnlineSessionState.Instance.SetRemotePeerPlayerId(playerId);
            var mode = OnlineSessionState.Instance.OnlineGameMode;
            var snapshot = alreadyReady
                ? OnlineSessionState.Instance.CurrentSetup?.Clone()
                : MatchSetupPersistence.LoadOrCreateOnline(mode, playerId, matchSetupPresetRegistry);
            if (snapshot == null) {
                OnlineSessionState.Instance.SetStatus("Failed to load online settings.");
                return;
            }

            snapshot.GameMode = mode;
            ApplyHostDraft(snapshot, broadcast: true);
            onlineSharedSetupReady = true;
            if (!alreadyReady) {
                lobbyUi?.ShowOnlineSharedSetupPanel(snapshot, isHost: true);
            }

            OnlineSessionState.Instance.SetStatus("Shared settings ready. Configure and start.");
        }

        void OnPlayerIdentityRequest() {
            if (OnlineSessionState.Instance == null
                || OnlineSessionState.Instance.PlayMode != OnlinePlayMode.OnlineClient
                || OnlineSessionState.Instance.IsMatchRunning) {
                return;
            }

            TrySendLocalPlayerIdentity();
        }

        void ApplyHostDraft(MatchSetupSnapshot snapshot, bool broadcast) {
            OnlineSessionState.Instance.SetCurrentSetup(snapshot);
            var peerId = OnlineSessionState.Instance.RemotePeerPlayerId;
            if (!string.IsNullOrEmpty(peerId)
                && !MatchSetupPersistence.TrySaveOnline(snapshot, peerId, matchSetupPresetRegistry, out var saveError)) {
                Debug.LogError($"OnlineSessionController: Failed to save online setup: {saveError}");
            }

            if (broadcast && messenger != null && matchSetupPresetRegistry != null) {
                var payload = MatchSetupNetworkCodec.ToPayload(snapshot, matchSetupPresetRegistry);
                messenger.BroadcastMatchSetup(payload);
            }
        }

        bool TryApplyPayload(
            MatchSetupNetworkPayload payload,
            out MatchSetupSnapshot snapshot,
            out string errorMessage) {
            snapshot = null;
            if (matchSetupPresetRegistry == null) {
                errorMessage = "MatchSetupPresetRegistry is not assigned.";
                return false;
            }

            return MatchSetupNetworkCodec.TryFromPayload(
                payload,
                matchSetupPresetRegistry,
                out snapshot,
                out errorMessage);
        }

        bool TryResolveOnlineMatchSetup(out MatchSetupSnapshot setup, out string errorMessage) {
            setup = OnlineSessionState.Instance?.CurrentSetup?.Clone();
            if (setup == null && matchSetupPresetRegistry != null) {
                var mode = OnlineSessionState.Instance.OnlineGameMode;
                var peerId = OnlineSessionState.Instance.RemotePeerPlayerId;
                setup = !string.IsNullOrEmpty(peerId)
                    ? MatchSetupPersistence.LoadOrCreateOnline(mode, peerId, matchSetupPresetRegistry)
                    : matchSetupPresetRegistry.CreateDefaultSnapshot(mode);
            }

            if (setup == null) {
                errorMessage = "MatchSetupPresetRegistry is not assigned.";
                return false;
            }

            setup.GameMode = OnlineSessionState.Instance.OnlineGameMode;
            if (!setup.TryValidate(matchSetupPresetRegistry, out errorMessage)) {
                setup = null;
                return false;
            }

            errorMessage = null;
            return true;
        }

        void EnsureMessenger(NetworkManager networkManager) {
            UnbindMessengerHandlers();
            messenger?.Dispose();
            messenger = new OnlineNetMessenger(networkManager);
            messenger.Register();
        }

        void BindHostMessengerHandlers() {
            if (messenger == null) {
                return;
            }

            messenger.MatchSetupUpdateReceived -= OnMatchSetupUpdateFromClient;
            messenger.PlayerIdentityReceived -= OnPlayerIdentity;
            messenger.MatchSetupUpdateReceived += OnMatchSetupUpdateFromClient;
            messenger.PlayerIdentityReceived += OnPlayerIdentity;
        }

        void BindClientMessengerHandlers() {
            if (messenger == null) {
                return;
            }

            messenger.MatchSetupReceived -= OnMatchSetupFromHost;
            messenger.MatchStartReceived -= OnMatchStartFromHost;
            messenger.MatchSetupBroadcastReceived -= OnMatchSetupBroadcast;
            messenger.PlayerIdentityRequestReceived -= OnPlayerIdentityRequest;
            messenger.MatchSetupReceived += OnMatchSetupFromHost;
            messenger.MatchStartReceived += OnMatchStartFromHost;
            messenger.MatchSetupBroadcastReceived += OnMatchSetupBroadcast;
            messenger.PlayerIdentityRequestReceived += OnPlayerIdentityRequest;
        }

        void UnbindMessengerHandlers() {
            if (messenger == null) {
                return;
            }

            messenger.MatchSetupReceived -= OnMatchSetupFromHost;
            messenger.MatchStartReceived -= OnMatchStartFromHost;
            messenger.MatchSetupBroadcastReceived -= OnMatchSetupBroadcast;
            messenger.MatchSetupUpdateReceived -= OnMatchSetupUpdateFromClient;
            messenger.PlayerIdentityReceived -= OnPlayerIdentity;
            messenger.PlayerIdentityRequestReceived -= OnPlayerIdentityRequest;
        }

        void BindNetworkCallbacks(NetworkManager networkManager) {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        void OnClientConnected(ulong clientId) {
            RefreshConnectedCount();
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null) {
                return;
            }

            if (networkManager.IsServer) {
                OnlineSessionState.Instance.SetStatus(
                    $"Connected {networkManager.ConnectedClientsList.Count}/{OnlineSessionConstants.MaxPlayers}");
                if (clientId != networkManager.LocalClientId
                    && !onlineSharedSetupReady
                    && !OnlineSessionState.Instance.IsMatchRunning) {
                    messenger?.RequestPlayerIdentityFromClient(clientId);
                    identityHandshakeTimer = OnlineSessionConstants.OnlineIdentityRetryIntervalSeconds;
                }

                return;
            }

            if (networkManager.IsConnectedClient
                && !OnlineSessionState.Instance.IsMatchRunning) {
                identityHandshakeTimer = 0f;
                TrySendLocalPlayerIdentity();
            }
        }

        void TickIdentityHandshake() {
            if (onlineSharedSetupReady
                || OnlineSessionState.Instance == null
                || OnlineSessionState.Instance.IsMatchRunning) {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening) {
                return;
            }

            identityHandshakeTimer -= Time.unscaledDeltaTime;
            if (identityHandshakeTimer > 0f) {
                return;
            }

            identityHandshakeTimer = OnlineSessionConstants.OnlineIdentityRetryIntervalSeconds;

            if (OnlineSessionState.Instance.PlayMode == OnlinePlayMode.OnlineClient
                && networkManager.IsConnectedClient
                && !networkManager.IsServer) {
                TrySendLocalPlayerIdentity();
                return;
            }

            if (OnlineSessionState.Instance.IsHost
                && networkManager.IsServer
                && networkManager.ConnectedClientsList.Count >= OnlineSessionConstants.MaxPlayers) {
                RequestIdentityFromRemoteClients(networkManager);
            }
        }

        void RequestIdentityFromRemoteClients(NetworkManager networkManager) {
            if (messenger == null) {
                return;
            }

            for (var i = 0; i < networkManager.ConnectedClientsList.Count; i++) {
                var clientId = networkManager.ConnectedClientsList[i].ClientId;
                if (clientId == networkManager.LocalClientId) {
                    continue;
                }

                messenger.RequestPlayerIdentityFromClient(clientId);
            }
        }

        void TrySendLocalPlayerIdentity() {
            if (!AuthenticationService.Instance.IsSignedIn) {
                return;
            }

            if (OnlineSessionState.Instance != null && OnlineSessionState.Instance.IsMatchRunning) {
                return;
            }

            var playerId = AuthenticationService.Instance.PlayerId;
            if (messenger == null || !messenger.TrySendPlayerIdentityToServer(playerId)) {
                return;
            }

            Debug.Log($"OnlineSessionController: Sent local player identity: {playerId}");
        }

        void OnClientDisconnected(ulong clientId) {
            RefreshConnectedCount();
            if (OnlineSessionState.Instance != null && OnlineSessionState.Instance.IsMatchRunning) {
                OnlineSessionState.Instance.SetStatus("Opponent disconnected.");
            }
        }

        void RefreshConnectedCount() {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening) {
                OnlineSessionState.Instance?.SetConnectedPlayerCount(0);
                return;
            }

            // ConnectedClientsList is server-only in Netcode for GameObjects.
            if (!networkManager.IsServer) {
                OnlineSessionState.Instance?.SetConnectedPlayerCount(
                    networkManager.IsConnectedClient ? OnlineSessionConstants.MaxPlayers : 0);
                return;
            }

            OnlineSessionState.Instance?.SetConnectedPlayerCount(
                networkManager.ConnectedClientsList.Count);
        }

        async Task SafeLeaveAsync() {
            onlineSharedSetupReady = false;
            UnbindMessengerHandlers();
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
