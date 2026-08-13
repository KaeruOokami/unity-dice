using System;
using System.Threading.Tasks;
using DiceGame.Config;
using DiceGame.Gameplay;
using DiceGame.Session.Network;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace DiceGame.Session
{
    [DefaultExecutionOrder(-100)]
    public sealed class SessionController : MonoBehaviour
    {
        [SerializeField] GameBootstrap gameBootstrap;
        [SerializeField] MatchSetupPresetRegistry matchSetupPresetRegistry;
        [SerializeField] UiFontSettings uiFontSettings;
        [SerializeField] bool showLobbyOnStart = true;

        readonly OnlineLobbyFacade lobbyFacade = new();
        OnlineNetMessenger messenger;
        TitleMenuUi titleMenuUi;
        bool busy;
        bool onlineSharedSetupReady;
        bool onlineGameModeConfirmed;
        float identityHandshakeTimer;
        bool waitingForMatchStartAck;
        MatchSetupNetworkPayload pendingMatchStartPayload;
        float matchStartAckRetryTimer;
        float matchStartAckWaitElapsed;

        public OnlineNetMessenger Messenger => messenger;
        public MatchSetupPresetRegistry MatchSetupPresetRegistry => matchSetupPresetRegistry;
        public PlayerInputSettings PlayerInputSettings => gameBootstrap != null
            ? gameBootstrap.PlayerInputSettings
            : matchSetupPresetRegistry != null
                ? matchSetupPresetRegistry.DefaultPlayerInputSettings
                : null;
        public UiFontSettings UiFontSettings => uiFontSettings;
        public bool IsOnlineSharedSetupReady => onlineSharedSetupReady;

        void Awake() {
            if (SessionState.Instance == null) {
                gameObject.AddComponent<SessionState>();
            }

            if (gameBootstrap == null) {
                gameBootstrap = FindFirstObjectByType<GameBootstrap>();
            }
        }

        void Start() {
            titleMenuUi = gameObject.GetComponent<TitleMenuUi>();
            if (titleMenuUi == null) {
                titleMenuUi = gameObject.AddComponent<TitleMenuUi>();
            }

            titleMenuUi.Configure(this, matchSetupPresetRegistry, uiFontSettings);
            PlayerBindingOverridesService.ApplyFromDisk(
                matchSetupPresetRegistry != null ? matchSetupPresetRegistry.DefaultPlayerInputSettings : null,
                gameBootstrap != null ? gameBootstrap.PlayerInputSettings : null);

            if (MatchFlowFlags.ConsumeSkipTitle(out var resumePlayMode)) {
                ResumeMatchAfterReload(resumePlayMode);
                return;
            }

            if (LaunchArgs.IsTrainingLaunch) {
                StartTrainingLocalPlay();
                return;
            }

            ResetToTitleState();

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
            TickMatchStartAckWait();
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
                SessionState.Instance.SetStatus("No local setup selected.");
                return;
            }

            if (matchSetupPresetRegistry == null) {
                SessionState.Instance.SetStatus("MatchSetupPresetRegistry is not assigned.");
                return;
            }

            if (!snapshot.TryValidate(matchSetupPresetRegistry, out var error)) {
                SessionState.Instance.SetStatus(error ?? "Invalid settings.");
                return;
            }

            if (snapshot.GameMode == GameMode.Challenge) {
                ChallengeRunState.Clear();
            }

            SessionState.Instance.SetCurrentSetup(snapshot);
            SessionState.Instance.SetPlayMode(SessionPlayMode.Local);
            SessionState.Instance.SetStatus("Starting local play.");
            SessionState.Instance.RequestMatchStart();
            ApplyLocalBindingOverrides();

            if (gameBootstrap != null && gameBootstrap.IsSessionActive) {
                ShowGameplayWorld();
                titleMenuUi?.Hide();
                return;
            }

            SessionState.Instance.ResetMatchFlag();
            SessionState.Instance.SetPlayMode(SessionPlayMode.Unspecified);
            HideGameplayWorld();
            titleMenuUi?.ShowMatchSetupPanel(snapshot.GameMode);
            SessionState.Instance.SetStatus("Failed to start the game. Check the Console.");
        }

        public async void CreateHostLobby() {
            if (busy) {
                return;
            }

            busy = true;
            onlineSharedSetupReady = false;
            onlineGameModeConfirmed = false;
            try {
                SessionState.Instance.SetPlayMode(SessionPlayMode.Host);
                SessionState.Instance.ClearRemotePeerPlayerId();
                SessionState.Instance.SetStatus("Authenticating...");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                SessionState.Instance.SetStatus("Reserving Relay...");
                var (allocation, relayJoinCode) = await OnlineRelayFacade.CreateAllocationAsync(
                    SessionConstants.MaxPlayers - 1);

                SessionState.Instance.SetStatus("Creating lobby...");
                var lobby = await lobbyFacade.CreateLobbyAsync(relayJoinCode, allocation.Region);
                SessionState.Instance.SetLobbyCode(lobby.LobbyCode);

                var networkManager = OnlineNetworkBootstrap.EnsureNetworkManager();
                var transport = OnlineRelayFacade.EnsureUnityTransport(networkManager);
                OnlineRelayFacade.ConfigureHostTransport(transport, allocation);

                BindNetworkCallbacks(networkManager);
                if (!networkManager.StartHost()) {
                    throw new InvalidOperationException("Failed to StartHost.");
                }

                EnsureMessenger(networkManager);
                BindHostMessengerHandlers();
                identityHandshakeTimer = SessionConstants.OnlineIdentityRetryIntervalSeconds;
                SessionState.Instance.SetStatus($"Host ready. Join code: {lobby.LobbyCode}");
                titleMenuUi?.ShowHostPanel(lobby.LobbyCode);
            } catch (Exception ex) {
                Debug.LogError($"SessionController: Host failed: {ex}");
                SessionState.Instance.SetStatus($"Host failed: {ex.Message}");
                SessionState.Instance.SetPlayMode(SessionPlayMode.Unspecified);
                await SafeLeaveAsync();
            } finally {
                busy = false;
            }
        }

        public async void ConfirmHostGameMode(GameMode mode) {
            if (busy) {
                return;
            }

            if (!SessionState.Instance.IsHost) {
                SessionState.Instance.SetStatus("Only the host can select the mode.");
                return;
            }

            if (mode != GameMode.Coop && mode != GameMode.Versus) {
                SessionState.Instance.SetStatus("Online mode must be Co-op or Versus.");
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.IsServer
                || networkManager.ConnectedClientsList.Count < SessionConstants.MaxPlayers) {
                SessionState.Instance.SetStatus("Waiting for a player to connect first.");
                titleMenuUi?.ShowHostPanel(SessionState.Instance.LobbyCode);
                return;
            }

            busy = true;
            try {
                SessionState.Instance.SetStatus("Updating lobby mode...");
                await lobbyFacade.UpdateLobbyGameModeAsync(mode);
                SessionState.Instance.SetLobbyGameMode(mode);
                onlineGameModeConfirmed = true;
                SessionState.Instance.SetStatus(
                    $"Mode set to {GameModeDisplayNames.GetDisplayName(mode)}. Preparing shared settings...");

                if (!string.IsNullOrEmpty(SessionState.Instance.RemotePeerPlayerId)) {
                    TryBeginSharedSetupFromIdentity(SessionState.Instance.RemotePeerPlayerId);
                } else {
                    RequestIdentityFromRemoteClients(networkManager);
                    identityHandshakeTimer = SessionConstants.OnlineIdentityRetryIntervalSeconds;
                    SessionState.Instance.SetStatus("Waiting for opponent identity...");
                }
            } catch (Exception ex) {
                Debug.LogError($"SessionController: ConfirmHostGameMode failed: {ex}");
                SessionState.Instance.SetStatus($"Failed to set mode: {ex.Message}");
                onlineGameModeConfirmed = false;
                titleMenuUi?.ShowOnlineModePanel();
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
            onlineGameModeConfirmed = false;
            try {
                SessionState.Instance.SetPlayMode(SessionPlayMode.Client);
                SessionState.Instance.ClearRemotePeerPlayerId();
                SessionState.Instance.SetStatus("Authenticating...");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                SessionState.Instance.SetStatus("Joining lobby...");
                var lobby = await lobbyFacade.JoinLobbyByCodeAsync(lobbyCode);
                SessionState.Instance.SetLobbyCode(lobby.LobbyCode);

                if (lobbyFacade.TryGetGameMode(out var mode)) {
                    SessionState.Instance.SetLobbyGameMode(mode);
                    onlineGameModeConfirmed = true;
                }

                if (!lobbyFacade.TryGetRelayJoinCode(out var relayJoinCode)) {
                    throw new InvalidOperationException("Lobby does not contain Relay join code.");
                }

                SessionState.Instance.SetStatus("Connecting to Relay...");
                var allocation = await OnlineRelayFacade.JoinAllocationAsync(relayJoinCode);

                var networkManager = OnlineNetworkBootstrap.EnsureNetworkManager();
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
                SessionState.Instance.SetStatus("Waiting for host to select mode...");
                titleMenuUi?.ShowClientWaitingForHostMode(lobby.LobbyCode);
            } catch (Exception ex) {
                Debug.LogError($"SessionController: Join failed: {ex}");
                SessionState.Instance.SetStatus($"Join failed: {ex.Message}");
                SessionState.Instance.SetPlayMode(SessionPlayMode.Unspecified);
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

            snapshot.GameMode = SessionState.Instance.LobbyGameMode;
            if (!snapshot.TryValidate(matchSetupPresetRegistry, out errorMessage)) {
                return false;
            }

            var payload = MatchSetupNetworkCodec.ToPayload(snapshot, matchSetupPresetRegistry);
            if (SessionState.Instance.IsHost) {
                ApplyHostDraft(snapshot, broadcast: true);
                return true;
            }

            messenger?.SendMatchSetupUpdateToServer(payload);
            SessionState.Instance.SetCurrentSetup(snapshot);
            return true;
        }

        public void StartOnlineMatchAsHost() {
            if (busy) {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) {
                SessionState.Instance.SetStatus("No host connection.");
                return;
            }

            if (networkManager.ConnectedClientsList.Count < SessionConstants.MaxPlayers) {
                SessionState.Instance.SetStatus(
                    $"Waiting for players ({networkManager.ConnectedClientsList.Count}/{SessionConstants.MaxPlayers})");
                return;
            }

            if (!onlineSharedSetupReady) {
                SessionState.Instance.SetStatus("Waiting for shared settings...");
                return;
            }

            if (waitingForMatchStartAck) {
                messenger?.SendMatchStartToClients(pendingMatchStartPayload);
                SessionState.Instance.SetStatus("Waiting for opponent ready...");
                return;
            }

            if (!TryResolveOnlineMatchSetup(out var setup, out var setupError)) {
                SessionState.Instance.SetStatus(setupError);
                return;
            }

            SessionState.Instance.SetCurrentSetup(setup);
            var payload = MatchSetupNetworkCodec.ToPayload(setup, matchSetupPresetRegistry);
            payload.MatchSeed = UnityEngine.Random.Range(1, int.MaxValue);
            SessionState.Instance.SetMatchSeed(payload.MatchSeed);

            if (messenger == null) {
                Debug.LogError(
                    "SessionController.StartOnlineMatchAsHost: messenger is null; " +
                    "MatchStart will not reach clients.");
                SessionState.Instance.SetStatus("Online messenger missing.");
                return;
            }

            pendingMatchStartPayload = payload;
            waitingForMatchStartAck = true;
            matchStartAckRetryTimer = SessionConstants.MatchStartAckRetryIntervalSeconds;
            matchStartAckWaitElapsed = 0f;

            Debug.Log(
                $"SessionController.StartOnlineMatchAsHost: " +
                $"clients={networkManager.ConnectedClientsList.Count} seed={payload.MatchSeed}");
            messenger.SendMatchStartToClients(payload);
            // Start host sim immediately so DualSim can receive client Prefill/inputs
            // while waiting for MatchStartAck (LockstepReady still gates Prefill exchange).
            onlineSharedSetupReady = false;
            SessionState.Instance.SetStatus("Waiting for opponent ready...");
            titleMenuUi?.Hide();
            ShowGameplayWorld();
            ApplyLocalBindingOverrides();
            SessionState.Instance.RequestMatchStart();
        }

        public async void ReturnToTitle() {
            await EndSessionAndReturnToTitle("Session ended.");
        }

        public void PrepareReturnToTitle() {
            ClearMatchStartHandshake();
            onlineSharedSetupReady = false;
            onlineGameModeConfirmed = false;
            MatchSeriesState.Clear();
            ChallengeRunState.Clear();
            SessionState.Instance?.ResetMatchFlag();
            SessionState.Instance?.ClearCurrentSetup();
            SessionState.Instance?.ClearRemotePeerPlayerId();
            SessionState.Instance?.SetPlayMode(SessionPlayMode.Unspecified);
            SessionState.Instance?.SetLobbyCode(string.Empty);
        }

        async Task EndSessionAndReturnToTitle(string statusMessage) {
            if (busy) {
                return;
            }

            busy = true;
            try {
                await SafeLeaveAsync();
                ClearMatchStartHandshake();
                onlineSharedSetupReady = false;
                onlineGameModeConfirmed = false;
                SessionState.Instance.ResetMatchFlag();
                SessionState.Instance.ClearCurrentSetup();
                SessionState.Instance.ClearRemotePeerPlayerId();
                SessionState.Instance.SetPlayMode(SessionPlayMode.Unspecified);
                SessionState.Instance.SetLobbyCode(string.Empty);
                ResetToTitleState();
                SessionState.Instance.SetStatus(statusMessage);
                titleMenuUi?.ShowMainPanel();
            } finally {
                busy = false;
            }
        }

        void StartTrainingLocalPlay() {
            if (matchSetupPresetRegistry == null) {
                Debug.LogError("SessionController: Training launch requires MatchSetupPresetRegistry.");
                ResetToTitleState();
                titleMenuUi?.ShowMainPanel();
                return;
            }

            var snapshot = matchSetupPresetRegistry.CreateDefaultSnapshot(LaunchArgs.TrainingGameMode);
            if (snapshot == null) {
                Debug.LogError("SessionController: Training launch setup is invalid. Snapshot is null.");
                ResetToTitleState();
                titleMenuUi?.ShowMainPanel();
                return;
            }

            LaunchArgs.ApplyTrainingPlayerSetup(snapshot);

            if (!snapshot.TryValidate(matchSetupPresetRegistry, out var error)) {
                Debug.LogError($"SessionController: Training launch setup is invalid. {error}");
                ResetToTitleState();
                titleMenuUi?.ShowMainPanel();
                return;
            }

            if (snapshot.GameMode == GameMode.Challenge) {
                ChallengeRunState.Clear();
            }

            SessionState.Instance.SetCurrentSetup(snapshot);
            SessionState.Instance.SetPlayMode(SessionPlayMode.Local);
            SessionState.Instance.SetStatus("Starting training play.");
            ShowGameplayWorld();
            titleMenuUi?.Hide();
            ApplyLocalBindingOverrides();
            SessionState.Instance.RequestMatchStart();
        }

        void ResumeMatchAfterReload(SessionPlayMode resumePlayMode) {
            SessionState.Instance.ResetMatchFlag();
            SessionState.Instance.SetPlayMode(resumePlayMode);
            var pendingSetup = MatchFlowFlags.ConsumePendingSetup();
            if (pendingSetup != null) {
                SessionState.Instance.SetCurrentSetup(pendingSetup);
            }

            var pendingSeed = MatchFlowFlags.ConsumePendingMatchSeed();
            if (pendingSeed != 0) {
                SessionState.Instance.SetMatchSeed(pendingSeed);
            }

            SessionState.Instance.SetStatus("Resuming match.");
            ShowGameplayWorld();
            titleMenuUi?.Hide();

            if (resumePlayMode == SessionPlayMode.Host
                || resumePlayMode == SessionPlayMode.Client) {
                var networkManager = NetworkManager.Singleton;
                if (networkManager == null || !networkManager.IsListening) {
                    Debug.LogError("SessionController: Online match restart requires an active NetworkManager.");
                    ResetToTitleState();
                    titleMenuUi?.ShowMainPanel();
                    return;
                }

                EnsureMessenger(networkManager);
                if (resumePlayMode == SessionPlayMode.Host) {
                    BindHostMessengerHandlers();
                } else {
                    BindClientMessengerHandlers();
                }
            }

            ApplyLocalBindingOverrides();
            SessionState.Instance.RequestMatchStart();
        }

        void ApplyLocalBindingOverrides() {
            PlayerBindingOverridesService.ApplyFromDisk(
                matchSetupPresetRegistry != null ? matchSetupPresetRegistry.DefaultPlayerInputSettings : null,
                gameBootstrap != null ? gameBootstrap.PlayerInputSettings : null);
        }

        void ResetToTitleState() {
            ClearMatchStartHandshake();
            onlineSharedSetupReady = false;
            onlineGameModeConfirmed = false;
            MatchSeriesState.Clear();
            ChallengeRunState.Clear();
            if (SessionState.Instance != null) {
                SessionState.Instance.SetPlayMode(SessionPlayMode.Unspecified);
                SessionState.Instance.ResetMatchFlag();
                SessionState.Instance.ClearCurrentSetup();
                SessionState.Instance.ClearRemotePeerPlayerId();
                SessionState.Instance.SetStatus("Choose local play, create a room, or join by code.");
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

            OnlineNetworkBootstrap.Shutdown();
            await lobbyFacade.LeaveAsync();
        }

        void ShowGameplayWorld() {
            var board = gameBootstrap != null ? gameBootstrap.Board : null;
            BoardVisibility.SetBoardVisible(board, true);
        }

        void HideGameplayWorld() {
            var board = gameBootstrap != null ? gameBootstrap.Board : null;
            BoardVisibility.SetBoardVisible(board, false);
        }

        void OnMatchStartFromHost() {
            if (SessionState.Instance.PlayMode != SessionPlayMode.Client) {
                return;
            }

            // Host may retry MatchStart; if presentation is already up, only re-ack.
            if (SessionState.Instance.IsMatchRunning
                || (gameBootstrap != null && gameBootstrap.IsSessionActive)) {
                messenger?.SendMatchStartAckToServer();
                return;
            }

            onlineSharedSetupReady = false;
            SessionState.Instance.SetStatus("Match starting");
            titleMenuUi?.Hide();
            ShowGameplayWorld();
            ApplyLocalBindingOverrides();
            SessionState.Instance.RequestMatchStart();
            messenger?.SendMatchStartAckToServer();
            Debug.Log("SessionController.OnMatchStartFromHost: presentation started, ack sent");
        }

        void OnMatchStartAckFromClient(ulong senderClientId) {
            if (!waitingForMatchStartAck) {
                return;
            }

            if (SessionState.Instance == null || !SessionState.Instance.IsHost) {
                return;
            }

            Debug.Log(
                $"SessionController.OnMatchStartAckFromClient: clientId={senderClientId}");
            ClearMatchStartHandshake();
            onlineSharedSetupReady = false;
            SessionState.Instance.SetStatus("Match starting");

            // Host sim already started when MatchStart was sent; ACK only completes handshake.
            if (gameBootstrap != null && gameBootstrap.IsSessionActive) {
                return;
            }

            if (SessionState.Instance.IsMatchRunning) {
                return;
            }

            titleMenuUi?.Hide();
            ShowGameplayWorld();
            SessionState.Instance.RequestMatchStart();
        }

        void TickMatchStartAckWait() {
            if (!waitingForMatchStartAck) {
                return;
            }

            if (SessionState.Instance == null || !SessionState.Instance.IsHost) {
                ClearMatchStartHandshake();
                return;
            }

            var dt = Time.unscaledDeltaTime;
            matchStartAckWaitElapsed += dt;
            if (matchStartAckWaitElapsed >= SessionConstants.MatchStartAckTimeoutSeconds) {
                Debug.LogError(
                    "SessionController: timed out waiting for MatchStartAck from client.");
                ClearMatchStartHandshake();
                SessionState.Instance.SetStatus("Opponent did not ready in time.");
                return;
            }

            matchStartAckRetryTimer -= dt;
            if (matchStartAckRetryTimer > 0f) {
                return;
            }

            matchStartAckRetryTimer = SessionConstants.MatchStartAckRetryIntervalSeconds;
            messenger?.SendMatchStartToClients(pendingMatchStartPayload);
            SessionState.Instance.SetStatus("Waiting for opponent ready...");
        }

        void ClearMatchStartHandshake() {
            waitingForMatchStartAck = false;
            matchStartAckRetryTimer = 0f;
            matchStartAckWaitElapsed = 0f;
            pendingMatchStartPayload = default;
        }

        void OnMatchSetupFromHost(MatchSetupNetworkPayload payload) {
            if (SessionState.Instance == null
                || SessionState.Instance.PlayMode != SessionPlayMode.Client) {
                return;
            }

            if (!TryApplyPayload(payload, out var snapshot, out var error)) {
                Debug.LogError($"SessionController: Failed to apply host setup: {error}");
                SessionState.Instance.SetStatus(error ?? "Failed to apply host settings.");
                return;
            }

            SessionState.Instance.SetCurrentSetup(snapshot);
            SessionState.Instance.SetLobbyGameMode(snapshot.GameMode);
            if (payload.MatchSeed != 0) {
                SessionState.Instance.SetMatchSeed(payload.MatchSeed);
            }

            SessionState.Instance.SetStatus("Received host settings.");
        }

        void OnMatchSetupBroadcast(MatchSetupNetworkPayload payload) {
            if (SessionState.Instance == null
                || SessionState.Instance.PlayMode != SessionPlayMode.Client
                || SessionState.Instance.IsMatchRunning) {
                return;
            }

            if (!TryApplyPayload(payload, out var snapshot, out var error)) {
                Debug.LogError($"SessionController: Failed to apply setup broadcast: {error}");
                SessionState.Instance.SetStatus(error ?? "Failed to apply shared settings.");
                return;
            }

            SessionState.Instance.SetCurrentSetup(snapshot);
            SessionState.Instance.SetLobbyGameMode(snapshot.GameMode);
            if (onlineSharedSetupReady) {
                titleMenuUi?.ApplyOnlineSetupFromRemote(snapshot);
            } else {
                onlineSharedSetupReady = true;
                titleMenuUi?.ShowOnlineSharedSetupPanel(snapshot, isHost: false);
            }

            SessionState.Instance.SetStatus("Shared settings ready.");
        }

        void OnMatchSetupUpdateFromClient(ulong senderClientId, MatchSetupNetworkPayload payload) {
            if (!SessionState.Instance.IsHost || SessionState.Instance.IsMatchRunning) {
                return;
            }

            if (!TryApplyPayload(payload, out var snapshot, out var error)) {
                Debug.LogError($"SessionController: Rejected client setup update: {error}");
                return;
            }

            snapshot.GameMode = SessionState.Instance.LobbyGameMode;
            ApplyHostDraft(snapshot, broadcast: true);
            titleMenuUi?.ApplyOnlineSetupFromRemote(snapshot);
        }

        void OnPlayerIdentity(ulong senderClientId, string playerId) {
            if (!SessionState.Instance.IsHost || SessionState.Instance.IsMatchRunning) {
                return;
            }

            if (string.IsNullOrWhiteSpace(playerId)) {
                Debug.LogError("SessionController: Received empty player identity.");
                return;
            }

            Debug.Log(
                $"SessionController: Received player identity from client {senderClientId}: {playerId}");

            SessionState.Instance.SetRemotePeerPlayerId(playerId);

            if (!onlineGameModeConfirmed) {
                SessionState.Instance.SetStatus("Player connected. Select Co-op or Versus.");
                titleMenuUi?.ShowOnlineModePanel();
                return;
            }

            TryBeginSharedSetupFromIdentity(playerId);
        }

        void TryBeginSharedSetupFromIdentity(string playerId) {
            if (!onlineGameModeConfirmed || string.IsNullOrWhiteSpace(playerId)) {
                return;
            }

            var alreadyReady =
                onlineSharedSetupReady
                && string.Equals(
                    SessionState.Instance.RemotePeerPlayerId,
                    playerId,
                    StringComparison.Ordinal);

            SessionState.Instance.SetRemotePeerPlayerId(playerId);
            var mode = SessionState.Instance.LobbyGameMode;
            var snapshot = alreadyReady
                ? SessionState.Instance.CurrentSetup?.Clone()
                : MatchSetupPersistence.LoadOrCreateOnline(mode, playerId, matchSetupPresetRegistry);
            if (snapshot == null) {
                SessionState.Instance.SetStatus("Failed to load online settings.");
                return;
            }

            snapshot.GameMode = mode;
            ApplyHostDraft(snapshot, broadcast: true);
            onlineSharedSetupReady = true;
            if (!alreadyReady) {
                titleMenuUi?.ShowOnlineSharedSetupPanel(snapshot, isHost: true);
            }

            SessionState.Instance.SetStatus("Shared settings ready. Configure and start.");
        }

        void OnPlayerIdentityRequest() {
            if (SessionState.Instance == null
                || SessionState.Instance.PlayMode != SessionPlayMode.Client
                || SessionState.Instance.IsMatchRunning) {
                return;
            }

            TrySendLocalPlayerIdentity();
        }

        void ApplyHostDraft(MatchSetupSnapshot snapshot, bool broadcast) {
            SessionState.Instance.SetCurrentSetup(snapshot);
            var peerId = SessionState.Instance.RemotePeerPlayerId;
            if (!string.IsNullOrEmpty(peerId)
                && !MatchSetupPersistence.TrySaveOnline(snapshot, peerId, matchSetupPresetRegistry, out var saveError)) {
                Debug.LogError($"SessionController: Failed to save online setup: {saveError}");
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
            setup = SessionState.Instance?.CurrentSetup?.Clone();
            if (setup == null && matchSetupPresetRegistry != null) {
                var mode = SessionState.Instance.LobbyGameMode;
                var peerId = SessionState.Instance.RemotePeerPlayerId;
                setup = !string.IsNullOrEmpty(peerId)
                    ? MatchSetupPersistence.LoadOrCreateOnline(mode, peerId, matchSetupPresetRegistry)
                    : matchSetupPresetRegistry.CreateDefaultSnapshot(mode);
            }

            if (setup == null) {
                errorMessage = "MatchSetupPresetRegistry is not assigned.";
                return false;
            }

            setup.GameMode = SessionState.Instance.LobbyGameMode;
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
            messenger.MatchStartAckReceived -= OnMatchStartAckFromClient;
            messenger.MatchSetupUpdateReceived += OnMatchSetupUpdateFromClient;
            messenger.PlayerIdentityReceived += OnPlayerIdentity;
            messenger.MatchStartAckReceived += OnMatchStartAckFromClient;
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
            messenger.MatchStartAckReceived -= OnMatchStartAckFromClient;
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
                SessionState.Instance.SetStatus(
                    $"Connected {networkManager.ConnectedClientsList.Count}/{SessionConstants.MaxPlayers}");
                if (clientId != networkManager.LocalClientId
                    && !SessionState.Instance.IsMatchRunning) {
                    if (!onlineGameModeConfirmed && !onlineSharedSetupReady) {
                        SessionState.Instance.SetStatus("Player connected. Select Co-op or Versus.");
                        titleMenuUi?.ShowOnlineModePanel();
                    }

                    if (!onlineSharedSetupReady) {
                        messenger?.RequestPlayerIdentityFromClient(clientId);
                        identityHandshakeTimer = SessionConstants.OnlineIdentityRetryIntervalSeconds;
                    }
                }

                return;
            }

            if (networkManager.IsConnectedClient
                && !SessionState.Instance.IsMatchRunning) {
                identityHandshakeTimer = 0f;
                TrySendLocalPlayerIdentity();
            }
        }

        void TickIdentityHandshake() {
            if (onlineSharedSetupReady
                || SessionState.Instance == null
                || SessionState.Instance.IsMatchRunning) {
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

            identityHandshakeTimer = SessionConstants.OnlineIdentityRetryIntervalSeconds;

            if (SessionState.Instance.PlayMode == SessionPlayMode.Client
                && networkManager.IsConnectedClient
                && !networkManager.IsServer) {
                TrySendLocalPlayerIdentity();
                return;
            }

            if (SessionState.Instance.IsHost
                && networkManager.IsServer
                && networkManager.ConnectedClientsList.Count >= SessionConstants.MaxPlayers) {
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

            if (SessionState.Instance != null && SessionState.Instance.IsMatchRunning) {
                return;
            }

            var playerId = AuthenticationService.Instance.PlayerId;
            if (messenger == null || !messenger.TrySendPlayerIdentityToServer(playerId)) {
                return;
            }

            Debug.Log($"SessionController: Sent local player identity: {playerId}");
        }

        void OnClientDisconnected(ulong clientId) {
            RefreshConnectedCount();
            if (SessionState.Instance == null) {
                return;
            }

            // Host closed the room (or connection dropped): client returns to title.
            if (SessionState.Instance.PlayMode == SessionPlayMode.Client) {
                if (busy) {
                    return;
                }

                _ = EndSessionAndReturnToTitle("Host closed the room.");
                return;
            }

            if (SessionState.Instance.IsMatchRunning) {
                SessionState.Instance.SetStatus("Opponent disconnected.");
                return;
            }

            if (!SessionState.Instance.IsHost) {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            if (networkManager.ConnectedClientsList.Count >= SessionConstants.MaxPlayers) {
                return;
            }

            onlineSharedSetupReady = false;
            onlineGameModeConfirmed = false;
            SessionState.Instance.ClearRemotePeerPlayerId();
            SessionState.Instance.ClearCurrentSetup();
            SessionState.Instance.SetStatus("Player disconnected. Waiting for a new player...");
            titleMenuUi?.ShowHostPanel(SessionState.Instance.LobbyCode);
        }

        void RefreshConnectedCount() {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening) {
                SessionState.Instance?.SetConnectedPlayerCount(0);
                return;
            }

            // ConnectedClientsList is server-only in Netcode for GameObjects.
            if (!networkManager.IsServer) {
                SessionState.Instance?.SetConnectedPlayerCount(
                    networkManager.IsConnectedClient ? SessionConstants.MaxPlayers : 0);
                return;
            }

            SessionState.Instance?.SetConnectedPlayerCount(
                networkManager.ConnectedClientsList.Count);
        }

        async Task SafeLeaveAsync() {
            onlineSharedSetupReady = false;
            onlineGameModeConfirmed = false;
            UnbindMessengerHandlers();
            if (messenger != null) {
                messenger.Dispose();
                messenger = null;
            }

            OnlineNetworkBootstrap.Shutdown();
            await lobbyFacade.LeaveAsync();
        }

        public GameBootstrap GameBootstrap => gameBootstrap;
    }
}
