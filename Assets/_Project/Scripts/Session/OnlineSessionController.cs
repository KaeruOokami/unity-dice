using System;
using System.Threading.Tasks;
using DiceGame.Gameplay;
using DiceGame.Session.Network;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session
{
    [DefaultExecutionOrder(-100)]
    public sealed class OnlineSessionController : MonoBehaviour
    {
        [SerializeField] GameBootstrap gameBootstrap;
        [SerializeField] bool showLobbyOnStart = true;

        readonly OnlineLobbyFacade lobbyFacade = new();
        OnlineNetMessenger messenger;
        OnlineLobbyUi lobbyUi;
        bool busy;

        public OnlineNetMessenger Messenger => messenger;

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

            lobbyUi.Configure(this);

            if (MatchFlowFlags.ConsumeSkipTitle(out var resumePlayMode)) {
                ResumeMatchAfterReload(resumePlayMode);
                return;
            }

            EnterTitlePresentation();

            if (!showLobbyOnStart) {
                StartLocalPlay();
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

        public void StartLocalPlay() {
            if (busy) {
                return;
            }

            OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Local);
            OnlineSessionState.Instance.SetStatus("ローカル対戦を開始します。");
            ShowGameplayWorld();
            OnlineSessionState.Instance.RequestMatchStart();
            lobbyUi?.Hide();
        }

        public async void CreateHostLobby() {
            if (busy) {
                return;
            }

            busy = true;
            try {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.OnlineHost);
                OnlineSessionState.Instance.SetStatus("認証中…");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                OnlineSessionState.Instance.SetStatus("Relay 確保中…");
                var (allocation, relayJoinCode) = await OnlineRelayFacade.CreateAllocationAsync(
                    OnlineSessionConstants.MaxPlayers - 1);

                OnlineSessionState.Instance.SetStatus("ロビー作成中…");
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
                    $"ホスト準備完了。参加コード: {lobby.LobbyCode}");
                lobbyUi?.ShowHostPanel(lobby.LobbyCode);
            } catch (Exception ex) {
                Debug.LogError($"OnlineSessionController: Host failed: {ex}");
                OnlineSessionState.Instance.SetStatus($"ホスト失敗: {ex.Message}");
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
                OnlineSessionState.Instance.SetStatus("認証中…");
                await UnityGamingServicesAuth.EnsureSignedInAsync();

                OnlineSessionState.Instance.SetStatus("ロビー参加中…");
                var lobby = await lobbyFacade.JoinLobbyByCodeAsync(lobbyCode);
                OnlineSessionState.Instance.SetLobbyCode(lobby.LobbyCode);

                if (!lobbyFacade.TryGetRelayJoinCode(out var relayJoinCode)) {
                    throw new InvalidOperationException("Lobby does not contain Relay join code.");
                }

                OnlineSessionState.Instance.SetStatus("Relay 接続中…");
                var allocation = await OnlineRelayFacade.JoinAllocationAsync(relayJoinCode);

                var networkManager = OnlineNetworkHost.EnsureNetworkManager();
                var transport = OnlineRelayFacade.EnsureUnityTransport(networkManager);
                OnlineRelayFacade.ConfigureClientTransport(transport, allocation);

                BindNetworkCallbacks(networkManager);
                if (!networkManager.StartClient()) {
                    throw new InvalidOperationException("Failed to StartClient.");
                }

                EnsureMessenger(networkManager);
                messenger.MatchStartReceived += OnMatchStartFromHost;
                OnlineSessionState.Instance.SetStatus("ホストの開始待ち…");
                lobbyUi?.ShowClientWaitingPanel(lobby.LobbyCode);
            } catch (Exception ex) {
                Debug.LogError($"OnlineSessionController: Join failed: {ex}");
                OnlineSessionState.Instance.SetStatus($"参加失敗: {ex.Message}");
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
                OnlineSessionState.Instance.SetStatus("ホスト接続がありません。");
                return;
            }

            if (networkManager.ConnectedClientsList.Count < OnlineSessionConstants.MaxPlayers) {
                OnlineSessionState.Instance.SetStatus(
                    $"参加者待ち ({networkManager.ConnectedClientsList.Count}/{OnlineSessionConstants.MaxPlayers})");
                return;
            }

            messenger?.SendMatchStartToClients();
            OnlineSessionState.Instance.SetStatus("試合開始");
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
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
                OnlineSessionState.Instance.SetLobbyCode(string.Empty);
                OnlineSessionState.Instance.SetStatus("セッションを終了しました。");
                EnterTitlePresentation();
                lobbyUi?.ShowMainPanel();
            } finally {
                busy = false;
            }
        }

        public void PrepareReturnToTitle() {
            OnlineSessionState.Instance?.ResetMatchFlag();
            OnlineSessionState.Instance?.SetPlayMode(OnlinePlayMode.Unspecified);
            OnlineSessionState.Instance?.SetLobbyCode(string.Empty);
        }

        void ResumeMatchAfterReload(OnlinePlayMode resumePlayMode) {
            OnlineSessionState.Instance.ResetMatchFlag();
            OnlineSessionState.Instance.SetPlayMode(resumePlayMode);
            OnlineSessionState.Instance.SetStatus("試合を再開します。");
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
                    messenger.MatchStartReceived += OnMatchStartFromHost;
                }
            }

            OnlineSessionState.Instance.RequestMatchStart();
        }

        void EnterTitlePresentation() {
            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.SetPlayMode(OnlinePlayMode.Unspecified);
                OnlineSessionState.Instance.ResetMatchFlag();
                OnlineSessionState.Instance.SetStatus("ローカル / ホスト作成 / コード参加を選んでください。");
            }

            HideGameplayWorld();
            _ = CleanupNetworkForTitleAsync();
        }

        async Task CleanupNetworkForTitleAsync() {
            if (messenger != null) {
                messenger.MatchStartReceived -= OnMatchStartFromHost;
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

            OnlineSessionState.Instance.SetStatus("試合開始");
            ShowGameplayWorld();
            OnlineSessionState.Instance.RequestMatchStart();
            lobbyUi?.Hide();
        }

        void EnsureMessenger(NetworkManager networkManager) {
            messenger?.Dispose();
            messenger = new OnlineNetMessenger(networkManager);
            messenger.Register();
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
                    $"接続数 {NetworkManager.Singleton.ConnectedClientsList.Count}/{OnlineSessionConstants.MaxPlayers}");
            }
        }

        void OnClientDisconnected(ulong clientId) {
            RefreshConnectedCount();
            if (OnlineSessionState.Instance != null && OnlineSessionState.Instance.IsMatchRunning) {
                OnlineSessionState.Instance.SetStatus("相手が切断しました。");
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
            if (messenger != null) {
                messenger.MatchStartReceived -= OnMatchStartFromHost;
                messenger.Dispose();
                messenger = null;
            }

            OnlineNetworkHost.Shutdown();
            await lobbyFacade.LeaveAsync();
        }

        public GameBootstrap GameBootstrap => gameBootstrap;
    }
}
