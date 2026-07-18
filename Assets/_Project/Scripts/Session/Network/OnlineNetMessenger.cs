using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    public sealed class OnlineNetMessenger : IDisposable
    {
        readonly NetworkManager networkManager;
        bool registered;

        public event Action<ulong, OnlineInputPayload> InputReceived;
        public event Action<OnlineMatchSnapshot> SnapshotReceived;
        public event Action MatchStartReceived;
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

            var size = 16 + (snapshot.Entities?.Length ?? 0) * 48;
            using var writer = new FastBufferWriter(Mathf.Max(size, 64), Allocator.Temp);
            writer.WriteNetworkSerializable(snapshot);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                OnlineSessionConstants.MessageSnapshot,
                writer,
                NetworkDelivery.UnreliableSequenced);
        }

        public void SendMatchStartToClients() {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(1);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                OnlineSessionConstants.MessageMatchStart,
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
            if (networkManager == null || !networkManager.IsClient || networkManager.IsServer) {
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
            if (networkManager.IsServer && !networkManager.IsClient) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineMatchSnapshot snapshot);
            SnapshotReceived?.Invoke(snapshot);
        }

        void OnMatchStartMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadValueSafe(out int _);
            MatchStartReceived?.Invoke();
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
