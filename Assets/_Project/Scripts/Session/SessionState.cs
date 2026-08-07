using System;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Session
{
    public sealed class SessionState : MonoBehaviour
    {
        public static SessionState Instance { get; private set; }

        public SessionPlayMode PlayMode { get; private set; } = SessionPlayMode.Unspecified;
        public string LobbyCode { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;
        public bool IsMatchRunning { get; private set; }
        public int ConnectedPlayerCount { get; private set; }
        public MatchSetupSnapshot CurrentSetup { get; private set; }
        public GameMode OnlineGameMode { get; private set; } = GameMode.Versus;
        public string RemotePeerPlayerId { get; private set; } = string.Empty;
        public int MatchSeed { get; private set; }

        public PlayerSlot LocalPlayerSlot =>
            PlayMode == SessionPlayMode.OnlineClient ? PlayerSlot.Player2 : PlayerSlot.Player1;

        public PlayerSlot RemotePlayerSlot =>
            LocalPlayerSlot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

        public bool IsOnline =>
            PlayMode == SessionPlayMode.OnlineHost || PlayMode == SessionPlayMode.OnlineClient;

        public bool IsHost => PlayMode == SessionPlayMode.OnlineHost;

        public event Action MatchStartRequested;
        public event Action StateChanged;

        void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }

        public void SetPlayMode(SessionPlayMode mode) {
            PlayMode = mode;
            RaiseStateChanged();
        }

        public void SetLobbyCode(string code) {
            LobbyCode = code ?? string.Empty;
            RaiseStateChanged();
        }

        public void SetStatus(string message) {
            StatusMessage = message ?? string.Empty;
            RaiseStateChanged();
        }

        public void SetConnectedPlayerCount(int count) {
            ConnectedPlayerCount = Mathf.Max(0, count);
            RaiseStateChanged();
        }

        public void SetCurrentSetup(MatchSetupSnapshot setup) {
            CurrentSetup = setup?.Clone();
            RaiseStateChanged();
        }

        public void ClearCurrentSetup() {
            CurrentSetup = null;
            RaiseStateChanged();
        }

        public void SetOnlineGameMode(GameMode mode) {
            OnlineGameMode = mode;
            RaiseStateChanged();
        }

        public void SetMatchSeed(int seed) {
            MatchSeed = seed;
            RaiseStateChanged();
        }

        public void SetRemotePeerPlayerId(string playerId) {
            RemotePeerPlayerId = playerId ?? string.Empty;
            RaiseStateChanged();
        }

        public void ClearRemotePeerPlayerId() {
            RemotePeerPlayerId = string.Empty;
            RaiseStateChanged();
        }

        public void RequestMatchStart() {
            if (IsMatchRunning) {
                return;
            }

            IsMatchRunning = true;
            RaiseStateChanged();
            MatchStartRequested?.Invoke();
        }

        public void ResetMatchFlag() {
            IsMatchRunning = false;
            RaiseStateChanged();
        }

        void RaiseStateChanged() {
            StateChanged?.Invoke();
        }
    }
}
