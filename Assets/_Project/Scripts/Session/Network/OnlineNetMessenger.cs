using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    public sealed class OnlineNetMessenger : IDisposable
    {
        const int MatchSetupWriterSize = 8192;
        const int IdentityWriterSize = 256;

        readonly NetworkManager networkManager;
        bool registered;
        uint snapshotSequence;
        float nextSnapshotSendLogTime;
        float nextSnapshotReceiveLogTime;

        public event Action<ulong, OnlineInputPayload> InputReceived;
        public event Action<OnlineMatchSnapshotChunk> SnapshotChunkReceived;
        public event Action MatchStartReceived;
        public event Action<MatchSetupNetworkPayload> MatchSetupReceived;
        public event Action<MatchSetupNetworkPayload> MatchSetupBroadcastReceived;
        public event Action<ulong, MatchSetupNetworkPayload> MatchSetupUpdateReceived;
        public event Action<ulong, string> PlayerIdentityReceived;
        public event Action PlayerIdentityRequestReceived;
        public event Action<byte> FlowCommandReceived;
        public event Action<ulong, byte> FlowRequestReceived;

        public OnlineNetMessenger(NetworkManager manager) {
            networkManager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void Register() {
            if (registered || networkManager.CustomMessagingManager == null) {
                return;
            }

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessageInput,
                OnInputMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessageSnapshot,
                OnSnapshotMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessageMatchStart,
                OnMatchStartMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessageMatchSetupBroadcast,
                OnMatchSetupBroadcastMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessageMatchSetupUpdate,
                OnMatchSetupUpdateMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessagePlayerIdentity,
                OnPlayerIdentityMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessagePlayerIdentityRequest,
                OnPlayerIdentityRequestMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessageFlowCommand,
                OnFlowCommandMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                OnlineSessionConstants.MessageFlowRequest,
                OnFlowRequestMessage);
            registered = true;
        }

        public void Dispose() {
            if (!registered || networkManager == null || networkManager.CustomMessagingManager == null) {
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessageInput);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessageSnapshot);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessageMatchStart);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessageMatchSetupBroadcast);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessageMatchSetupUpdate);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessagePlayerIdentity);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessagePlayerIdentityRequest);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessageFlowCommand);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                OnlineSessionConstants.MessageFlowRequest);
            registered = false;
        }

        public void SendInputToServer(OnlineInputPayload payload) {
            if (networkManager == null || !networkManager.IsClient) {
                return;
            }

            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteNetworkSerializable(payload);
            networkManager.CustomMessagingManager.SendNamedMessage(
                OnlineSessionConstants.MessageInput,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.UnreliableSequenced);
        }

        public void SendSnapshotToClients(OnlineMatchSnapshot snapshot) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null) {
                Debug.LogWarning("OnlineNetMessenger.SendSnapshotToClients: CustomMessagingManager is null.");
                return;
            }

            if (!networkManager.IsListening || networkManager.ConnectedClientsIds.Count <= 1) {
                LogSnapshotSendThrottled(
                    $"skip: no remote clients (connected={networkManager.ConnectedClientsIds.Count})");
                return;
            }

            var entities = snapshot.Entities ?? System.Array.Empty<OnlineTransformSnapshot>();
            var maxPerChunk = Mathf.Max(1, OnlineSessionConstants.SnapshotMaxEntitiesPerChunk);
            var chunkCount = entities.Length == 0
                ? 1
                : (entities.Length + maxPerChunk - 1) / maxPerChunk;
            while (chunkCount > 64) {
                maxPerChunk++;
                chunkCount = (entities.Length + maxPerChunk - 1) / maxPerChunk;
            }

            snapshotSequence++;
            var sequence = snapshotSequence;
            var offset = 0;
            // UnreliableSequenced drops sibling chunks; use unordered Unreliable + sequence/mask reassembly.
            const NetworkDelivery delivery = NetworkDelivery.Unreliable;
            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++) {
                var count = entities.Length == 0
                    ? 0
                    : Mathf.Min(maxPerChunk, entities.Length - offset);
                var chunkEntities = count > 0
                    ? new OnlineTransformSnapshot[count]
                    : System.Array.Empty<OnlineTransformSnapshot>();
                if (count > 0) {
                    System.Array.Copy(entities, offset, chunkEntities, 0, count);
                    offset += count;
                }

                var chunk = new OnlineMatchSnapshotChunk {
                    Sequence = sequence,
                    ChunkIndex = (ushort)chunkIndex,
                    ChunkCount = (ushort)chunkCount,
                    Entities = chunkEntities
                };

                using var writer = new FastBufferWriter(512, Allocator.Temp, 2048);
                writer.WriteNetworkSerializable(chunk);
                if (writer.Length > 1200) {
                    Debug.LogWarning(
                        $"OnlineNetMessenger.SendSnapshotToClients: chunk {chunkIndex}/{chunkCount} is {writer.Length} bytes (may exceed Unreliable MTU).");
                }

                customMessaging.SendNamedMessageToAll(
                    OnlineSessionConstants.MessageSnapshot,
                    writer,
                    delivery);
            }

            LogSnapshotSendThrottled(
                $"send ToAll entities={entities.Length} chunks={chunkCount} perChunk={maxPerChunk} " +
                $"seq={sequence} delivery={delivery} interval={OnlineSessionConstants.SnapshotSendIntervalSeconds:0.###}s");
        }

        void LogSnapshotSendThrottled(string message) {
            var now = Time.realtimeSinceStartup;
            if (now < nextSnapshotSendLogTime) {
                return;
            }

            nextSnapshotSendLogTime = now + 2f;
            Debug.Log($"OnlineNetMessenger.SendSnapshotToClients: {message}");
        }

        public void SendMatchStartToClients(MatchSetupNetworkPayload setupPayload) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(MatchSetupWriterSize, Allocator.Temp);
            writer.WriteNetworkSerializable(setupPayload);
            writer.WriteValueSafe(1);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                OnlineSessionConstants.MessageMatchStart,
                writer,
                NetworkDelivery.Reliable);
        }

        public void BroadcastMatchSetup(MatchSetupNetworkPayload setupPayload) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(MatchSetupWriterSize, Allocator.Temp);
            writer.WriteNetworkSerializable(setupPayload);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                OnlineSessionConstants.MessageMatchSetupBroadcast,
                writer,
                NetworkDelivery.Reliable);
        }

        public void SendMatchSetupUpdateToServer(MatchSetupNetworkPayload setupPayload) {
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(MatchSetupWriterSize, Allocator.Temp);
            writer.WriteNetworkSerializable(setupPayload);
            networkManager.CustomMessagingManager.SendNamedMessage(
                OnlineSessionConstants.MessageMatchSetupUpdate,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.Reliable);
        }

        public bool TrySendPlayerIdentityToServer(string playerId) {
            if (networkManager == null
                || !networkManager.IsConnectedClient
                || networkManager.IsServer
                || networkManager.CustomMessagingManager == null) {
                return false;
            }

            if (string.IsNullOrEmpty(playerId)) {
                return false;
            }

            var fixedId = new FixedString128Bytes(playerId);
            using var writer = new FastBufferWriter(IdentityWriterSize, Allocator.Temp);
            writer.WriteValueSafe(fixedId);
            networkManager.CustomMessagingManager.SendNamedMessage(
                OnlineSessionConstants.MessagePlayerIdentity,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.Reliable);
            return true;
        }

        public void RequestPlayerIdentityFromClient(ulong clientId) {
            if (networkManager == null
                || !networkManager.IsServer
                || networkManager.CustomMessagingManager == null) {
                return;
            }

            if (clientId == NetworkManager.ServerClientId) {
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            networkManager.CustomMessagingManager.SendNamedMessage(
                OnlineSessionConstants.MessagePlayerIdentityRequest,
                clientId,
                writer,
                NetworkDelivery.Reliable);
        }

        public void BroadcastFlowCommand(byte command) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(command);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                OnlineSessionConstants.MessageFlowCommand,
                writer,
                NetworkDelivery.Reliable);
        }

        public void SendFlowRequestToServer(byte command) {
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(command);
            networkManager.CustomMessagingManager.SendNamedMessage(
                OnlineSessionConstants.MessageFlowRequest,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.Reliable);
        }

        void OnInputMessage(ulong senderClientId, FastBufferReader reader) {
            if (!networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineInputPayload payload);
            InputReceived?.Invoke(senderClientId, payload);
        }

        void OnSnapshotMessage(ulong senderClientId, FastBufferReader reader) {
            // Host loopback from SendNamedMessageToAll; presentation is client-only.
            if (networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineMatchSnapshotChunk chunk);

            var now = Time.realtimeSinceStartup;
            if (now >= nextSnapshotReceiveLogTime) {
                nextSnapshotReceiveLogTime = now + 2f;
                Debug.Log(
                    $"OnlineNetMessenger.OnSnapshotMessage: sender={senderClientId} " +
                    $"seq={chunk.Sequence} chunk={chunk.ChunkIndex}/{chunk.ChunkCount} " +
                    $"entities={chunk.Entities?.Length ?? 0} " +
                    $"subscribers={SnapshotChunkReceived?.GetInvocationList().Length ?? 0}");
            }

            SnapshotChunkReceived?.Invoke(chunk);
        }

        void OnMatchStartMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadNetworkSerializable(out MatchSetupNetworkPayload setupPayload);
            reader.ReadValueSafe(out int _);
            MatchSetupReceived?.Invoke(setupPayload);
            MatchStartReceived?.Invoke();
        }

        void OnMatchSetupBroadcastMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadNetworkSerializable(out MatchSetupNetworkPayload setupPayload);
            MatchSetupBroadcastReceived?.Invoke(setupPayload);
        }

        void OnMatchSetupUpdateMessage(ulong senderClientId, FastBufferReader reader) {
            if (!networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out MatchSetupNetworkPayload setupPayload);
            MatchSetupUpdateReceived?.Invoke(senderClientId, setupPayload);
        }

        void OnPlayerIdentityMessage(ulong senderClientId, FastBufferReader reader) {
            if (!networkManager.IsServer) {
                return;
            }

            reader.ReadValueSafe(out FixedString128Bytes fixedId);
            PlayerIdentityReceived?.Invoke(senderClientId, fixedId.ToString());
        }

        void OnPlayerIdentityRequestMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadValueSafe(out byte _);
            PlayerIdentityRequestReceived?.Invoke();
        }

        void OnFlowCommandMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadValueSafe(out byte command);
            FlowCommandReceived?.Invoke(command);
        }

        void OnFlowRequestMessage(ulong senderClientId, FastBufferReader reader) {
            if (!networkManager.IsServer) {
                return;
            }

            reader.ReadValueSafe(out byte command);
            FlowRequestReceived?.Invoke(senderClientId, command);
        }
    }
}
