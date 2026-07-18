using System;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Session
{
    public sealed class OnlineSessionState : MonoBehaviour
    {
        public static OnlineSessionState Instance { get; private set; }

        public OnlinePlayMode PlayMode { get; private set; } = OnlinePlayMode.Unspecified;
        public string LobbyCode { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;
        public bool IsMatchRunning { get; private set; }
        public int ConnectedPlayerCount { get; private set; }

        public PlayerSlot LocalPlayerSlot =>
            PlayMode == OnlinePlayMode.OnlineClient ? PlayerSlot.Player2 : PlayerSlot.Player1;

        public PlayerSlot RemotePlayerSlot =>
            LocalPlayerSlot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;

        public bool IsOnline =>
            PlayMode == OnlinePlayMode.OnlineHost || PlayMode == OnlinePlayMode.OnlineClient;

        public bool IsHost => PlayMode == OnlinePlayMode.OnlineHost;

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

        public void SetPlayMode(OnlinePlayMode mode) {
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
