using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace DiceGame.Session
{
    public sealed class OnlineLobbyFacade
    {
        Lobby hostLobby;
        Lobby joinedLobby;
        float heartbeatTimer;

        public Lobby ActiveLobby => hostLobby ?? joinedLobby;
        public string LobbyCode => ActiveLobby?.LobbyCode ?? string.Empty;
        public bool IsHost => hostLobby != null;

        public async Task<Lobby> CreateLobbyAsync(string relayJoinCode, string relayRegion) {
            await UnityGamingServicesAuth.EnsureSignedInAsync();

            var options = new CreateLobbyOptions {
                IsPrivate = true,
                Data = new Dictionary<string, DataObject> {
                    {
                        OnlineSessionConstants.LobbyDataRelayJoinCode,
                        new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                    },
                    {
                        OnlineSessionConstants.LobbyDataRelayRegion,
                        new DataObject(DataObject.VisibilityOptions.Member, relayRegion ?? string.Empty)
                    }
                }
            };

            hostLobby = await LobbyService.Instance.CreateLobbyAsync(
                $"Dice_{Guid.NewGuid():N}".Substring(0, 12),
                OnlineSessionConstants.MaxPlayers,
                options);
            joinedLobby = null;
            heartbeatTimer = 0f;
            return hostLobby;
        }

        public async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode) {
            await UnityGamingServicesAuth.EnsureSignedInAsync();

            if (string.IsNullOrWhiteSpace(lobbyCode)) {
                throw new InvalidOperationException("Lobby code is empty.");
            }

            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim().ToUpperInvariant());
            hostLobby = null;
            return joinedLobby;
        }

        public bool TryGetRelayJoinCode(out string relayJoinCode) {
            relayJoinCode = null;
            var lobby = ActiveLobby;
            if (lobby?.Data == null) {
                return false;
            }

            if (!lobby.Data.TryGetValue(OnlineSessionConstants.LobbyDataRelayJoinCode, out var data)
                || string.IsNullOrEmpty(data?.Value)) {
                return false;
            }

            relayJoinCode = data.Value;
            return true;
        }

        public async Task TickHeartbeatAsync(float deltaTime) {
            if (hostLobby == null) {
                return;
            }

            heartbeatTimer += deltaTime;
            if (heartbeatTimer < OnlineSessionConstants.LobbyHeartbeatSeconds) {
                return;
            }

            heartbeatTimer = 0f;
            try {
                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            } catch (Exception ex) {
                Debug.LogWarning($"OnlineLobbyFacade: Heartbeat failed: {ex.Message}");
            }
        }

        public async Task RefreshAsync() {
            var lobby = ActiveLobby;
            if (lobby == null) {
                return;
            }

            var refreshed = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
            if (hostLobby != null) {
                hostLobby = refreshed;
            } else {
                joinedLobby = refreshed;
            }
        }

        public async Task LeaveAsync() {
            var lobby = ActiveLobby;
            if (lobby == null) {
                return;
            }

            try {
                if (hostLobby != null) {
                    await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
                } else {
                    await LobbyService.Instance.RemovePlayerAsync(
                        lobby.Id,
                        AuthenticationPlayerId());
                }
            } catch (Exception ex) {
                Debug.LogWarning($"OnlineLobbyFacade: Leave failed: {ex.Message}");
            } finally {
                hostLobby = null;
                joinedLobby = null;
            }
        }

        static string AuthenticationPlayerId() {
            return Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        }
    }
}
