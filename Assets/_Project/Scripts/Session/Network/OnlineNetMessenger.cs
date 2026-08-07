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
        const int InputBatchHeaderBytes = 8;
        const int InputPayloadMaxBytes = 32;

        readonly NetworkManager networkManager;
        bool registered;
        uint snapshotSequence;
        float nextSnapshotSendLogTime;
        float nextSnapshotReceiveLogTime;
        float nextAttackQueueSendLogTime;

        public event Action<ulong, OnlineInputBatchPayload> InputReceived;
        public event Action<OnlineInputBatchPayload> HostInputReceived;
        public event Action<OnlineMatchSnapshotChunk> SnapshotChunkReceived;
        public event Action<OnlineDiceMotionEvent> DiceMotionReceived;
        public event Action<OnlineAttackQueueSnapshot> AttackQueueReceived;
        public event Action<OnlineDiceSpawnCommand> DiceSpawnReceived;
        public event Action<OnlineCharacterStateBatch> CharacterStateReceived;
        public event Action MatchStartReceived;
        public event Action<ulong> MatchStartAckReceived;
        public event Action<MatchSetupPayload> MatchSetupReceived;
        public event Action<MatchSetupPayload> MatchSetupBroadcastReceived;
        public event Action<ulong, MatchSetupPayload> MatchSetupUpdateReceived;
        public event Action<ulong, string> PlayerIdentityReceived;
        public event Action PlayerIdentityRequestReceived;
        public event Action<byte, int> FlowCommandReceived;
        public event Action<ulong, byte> FlowRequestReceived;
        public event Action LockstepReadyReceived;
        public event Action<ulong> LockstepReadyFromClient;
        public event Action<OnlineSimHashPayload> SimHashReceived;
        public event Action<OnlineSimResyncPayload> SimResyncReceived;

        public OnlineNetMessenger(NetworkManager manager) {
            networkManager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void Register() {
            if (registered || networkManager.CustomMessagingManager == null) {
                return;
            }

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageInput,
                OnInputMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageSnapshot,
                OnSnapshotMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageDiceMotion,
                OnDiceMotionMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageAttackQueue,
                OnAttackQueueMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageDiceSpawn,
                OnDiceSpawnMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageCharacterState,
                OnCharacterStateMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageMatchStart,
                OnMatchStartMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageMatchStartAck,
                OnMatchStartAckMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageMatchSetupBroadcast,
                OnMatchSetupBroadcastMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageMatchSetupUpdate,
                OnMatchSetupUpdateMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessagePlayerIdentity,
                OnPlayerIdentityMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessagePlayerIdentityRequest,
                OnPlayerIdentityRequestMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageFlowCommand,
                OnFlowCommandMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageFlowRequest,
                OnFlowRequestMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageLockstepReady,
                OnLockstepReadyMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageSimHash,
                OnSimHashMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                SessionConstants.MessageSimResync,
                OnSimResyncMessage);
            registered = true;
        }

        public void Dispose() {
            if (!registered || networkManager == null || networkManager.CustomMessagingManager == null) {
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageInput);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageSnapshot);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageDiceMotion);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageAttackQueue);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageDiceSpawn);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageCharacterState);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageMatchStart);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageMatchStartAck);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageMatchSetupBroadcast);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageMatchSetupUpdate);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessagePlayerIdentity);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessagePlayerIdentityRequest);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageFlowCommand);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageFlowRequest);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageLockstepReady);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageSimHash);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                SessionConstants.MessageSimResync);
            registered = false;
        }

        public void SendInputToServer(OnlineInputPayload payload) {
            SendInputToServer(new OnlineInputBatchPayload {
                Inputs = new[] { payload }
            });
        }

        public void SendInputToServer(OnlineInputBatchPayload payload) {
            if (networkManager == null || !networkManager.IsClient) {
                return;
            }

            if (!TryValidateInputBatchSize(payload, nameof(SendInputToServer))) {
                return;
            }

            using var writer = new FastBufferWriter(InputBatchWriterSize(payload), Allocator.Temp);
            writer.WriteNetworkSerializable(payload);
            networkManager.CustomMessagingManager.SendNamedMessage(
                SessionConstants.MessageInput,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.UnreliableSequenced);
        }

        public void SendInputToClients(OnlineInputPayload payload) {
            SendInputToClients(new OnlineInputBatchPayload {
                Inputs = new[] { payload }
            });
        }

        public void SendInputToClients(OnlineInputBatchPayload payload) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null || !networkManager.IsListening) {
                return;
            }

            if (!TryValidateInputBatchSize(payload, nameof(SendInputToClients))) {
                return;
            }

            var localId = networkManager.LocalClientId;
            foreach (var clientId in networkManager.ConnectedClientsIds) {
                if (clientId == localId) {
                    continue;
                }

                using var writer = new FastBufferWriter(InputBatchWriterSize(payload), Allocator.Temp);
                writer.WriteNetworkSerializable(payload);
                customMessaging.SendNamedMessage(
                    SessionConstants.MessageInput,
                    clientId,
                    writer,
                    NetworkDelivery.UnreliableSequenced);
            }
        }

        static bool TryValidateInputBatchSize(OnlineInputBatchPayload payload, string caller) {
            var count = payload.Inputs?.Length ?? 0;
            var max = SessionConstants.LockstepInputMaxPayloadsPerBatch;
            if (count <= max) {
                return true;
            }

            Debug.LogError(
                $"OnlineNetMessenger.{caller}: input batch count={count} exceeds " +
                $"LockstepInputMaxPayloadsPerBatch={max}. Split before send.");
            return false;
        }

        static int InputBatchWriterSize(OnlineInputBatchPayload payload) {
            var count = payload.Inputs?.Length ?? 0;
            return InputBatchHeaderBytes + count * InputPayloadMaxBytes;
        }

        public void SendDiceSpawnToClients(OnlineDiceSpawnCommand command) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null) {
                return;
            }

            if (!networkManager.IsListening || networkManager.ConnectedClientsIds.Count <= 1) {
                return;
            }

            using var writer = new FastBufferWriter(64, Allocator.Temp, 256);
            writer.WriteNetworkSerializable(command);
            customMessaging.SendNamedMessageToAll(
                SessionConstants.MessageDiceSpawn,
                writer,
                NetworkDelivery.Reliable);
            Debug.Log(
                $"OnlineNetMessenger.SendDiceSpawnToClients: reason={command.Reason} " +
                $"kind={command.Kind} cell=({command.GridX},{command.GridY}) owner={command.OwnerSlot}");
        }

        public bool HasRemoteClients() {
            return networkManager != null
                && networkManager.IsListening
                && networkManager.ConnectedClientsIds.Count > 1;
        }

        public void SendCharacterStateToClients(OnlineCharacterStateBatch batch) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null) {
                return;
            }

            if (!networkManager.IsListening || networkManager.ConnectedClientsIds.Count <= 1) {
                return;
            }

            using var writer = new FastBufferWriter(128, Allocator.Temp, 512);
            writer.WriteNetworkSerializable(batch);
            customMessaging.SendNamedMessageToAll(
                SessionConstants.MessageCharacterState,
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
            snapshotSequence++;
            var chunk = new OnlineMatchSnapshotChunk {
                Sequence = snapshotSequence,
                ChunkIndex = 0,
                ChunkCount = 1,
                Entities = entities
            };

            // Phase A: full board may exceed non-fragmented Reliable MTU (~1264).
            // Use fragmented reliable so rare dumps are not dropped.
            var localId = networkManager.LocalClientId;
            var sent = 0;
            foreach (var clientId in networkManager.ConnectedClientsIds) {
                if (clientId == localId) {
                    continue;
                }

                using var writer = new FastBufferWriter(1024, Allocator.Temp, 8192);
                writer.WriteNetworkSerializable(chunk);
                if (writer.Length > SessionConstants.SnapshotReliableSoftBytes) {
                    Debug.LogWarning(
                        $"OnlineNetMessenger.SendSnapshotToClients: snapshot is {writer.Length} bytes " +
                        $"(soft limit {SessionConstants.SnapshotReliableSoftBytes}). Keep shrinking payload.");
                }

                customMessaging.SendNamedMessage(
                    SessionConstants.MessageSnapshot,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
                sent++;
            }

            if (sent == 0) {
                return;
            }

            LogSnapshotSendThrottled(
                $"send remotes={sent} entities={entities.Length} chunks=1 " +
                $"seq={snapshotSequence} delivery=ReliableFragmentedSequenced " +
                $"interval={SessionConstants.SnapshotSendIntervalSeconds:0.###}s");
        }

        public void SendDiceMotionToClients(OnlineDiceMotionEvent motionEvent) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null || !networkManager.IsListening) {
                return;
            }

            var localId = networkManager.LocalClientId;
            var sent = 0;
            foreach (var clientId in networkManager.ConnectedClientsIds) {
                if (clientId == localId) {
                    continue;
                }

                using var writer = new FastBufferWriter(128, Allocator.Temp, 512);
                writer.WriteNetworkSerializable(motionEvent);
                customMessaging.SendNamedMessage(
                    SessionConstants.MessageDiceMotion,
                    clientId,
                    writer,
                    NetworkDelivery.Reliable);
                sent++;
            }

            if (sent == 0) {
                return;
            }

            Debug.Log(
                $"OnlineNetMessenger.SendDiceMotionToClients: entity={motionEvent.EntityId} " +
                $"kind={motionEvent.Kind} catalogSide={motionEvent.CatalogSide} remotes={sent}");
        }

        public void SendAttackQueueToClients(OnlineAttackQueueSnapshot queueSnapshot) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null || !networkManager.IsListening) {
                return;
            }

            var localId = networkManager.LocalClientId;
            var sent = 0;
            foreach (var clientId in networkManager.ConnectedClientsIds) {
                if (clientId == localId) {
                    continue;
                }

                using var writer = new FastBufferWriter(256, Allocator.Temp, 2048);
                writer.WriteNetworkSerializable(queueSnapshot);
                customMessaging.SendNamedMessage(
                    SessionConstants.MessageAttackQueue,
                    clientId,
                    writer,
                    NetworkDelivery.Reliable);
                sent++;
            }

            if (sent == 0) {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now >= nextAttackQueueSendLogTime) {
                nextAttackQueueSendLogTime = now + 2f;
                Debug.Log(
                    $"OnlineNetMessenger.SendAttackQueueToClients: " +
                    $"p1={queueSnapshot.Player1Volleys?.Length ?? 0} p2={queueSnapshot.Player2Volleys?.Length ?? 0} " +
                    $"remotes={sent}");
            }
        }

        void LogSnapshotSendThrottled(string message) {
            var now = Time.realtimeSinceStartup;
            if (now < nextSnapshotSendLogTime) {
                return;
            }

            nextSnapshotSendLogTime = now + 2f;
            Debug.Log($"OnlineNetMessenger.SendSnapshotToClients: {message}");
        }

        public void SendMatchStartToClients(MatchSetupPayload setupPayload) {
            if (networkManager == null || !networkManager.IsServer) {
                Debug.LogWarning(
                    $"OnlineNetMessenger.SendMatchStartToClients: skipped " +
                    $"(nmNull={networkManager == null} isServer={networkManager != null && networkManager.IsServer})");
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null) {
                Debug.LogError("OnlineNetMessenger.SendMatchStartToClients: CustomMessagingManager is null.");
                return;
            }

            if (!networkManager.IsListening) {
                Debug.LogError("OnlineNetMessenger.SendMatchStartToClients: NetworkManager is not listening.");
                return;
            }

            var matchSeed = setupPayload.MatchSeed != 0
                ? setupPayload.MatchSeed
                : 1;
            var localId = networkManager.LocalClientId;
            var sent = 0;
            foreach (var clientId in networkManager.ConnectedClientsIds) {
                if (clientId == localId) {
                    continue;
                }

                using var writer = new FastBufferWriter(MatchSetupWriterSize, Allocator.Temp, MatchSetupWriterSize);
                writer.WriteNetworkSerializable(setupPayload);
                // Trailing int: match RNG seed.
                writer.WriteValueSafe(matchSeed);
                try {
                    customMessaging.SendNamedMessage(
                        SessionConstants.MessageMatchStart,
                        clientId,
                        writer,
                        NetworkDelivery.Reliable);
                    sent++;
                    Debug.Log(
                        $"OnlineNetMessenger.SendMatchStartToClients: sent to clientId={clientId} " +
                        $"bytes={writer.Length} seed={matchSeed} delivery=Reliable");
                } catch (System.Exception ex) {
                    Debug.LogError(
                        $"OnlineNetMessenger.SendMatchStartToClients: send to {clientId} failed: {ex}");
                }
            }

            if (sent == 0) {
                Debug.LogWarning(
                    "OnlineNetMessenger.SendMatchStartToClients: no remote clients to send to.");
            }
        }

        public void SendMatchStartAckToServer() {
            if (networkManager == null || !networkManager.IsClient || networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null) {
                Debug.LogError("OnlineNetMessenger.SendMatchStartAckToServer: CustomMessagingManager is null.");
                return;
            }

            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            customMessaging.SendNamedMessage(
                SessionConstants.MessageMatchStartAck,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.Reliable);
            Debug.Log("OnlineNetMessenger.SendMatchStartAckToServer: sent");
        }

        public void SendLockstepReadyToServer() {
            if (networkManager == null || !networkManager.IsClient || networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null) {
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            customMessaging.SendNamedMessage(
                SessionConstants.MessageLockstepReady,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.Reliable);
        }

        public void SendLockstepReadyToClients() {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null || !networkManager.IsListening) {
                return;
            }

            var localId = networkManager.LocalClientId;
            foreach (var clientId in networkManager.ConnectedClientsIds) {
                if (clientId == localId) {
                    continue;
                }

                using var writer = new FastBufferWriter(8, Allocator.Temp);
                writer.WriteValueSafe((byte)1);
                customMessaging.SendNamedMessage(
                    SessionConstants.MessageLockstepReady,
                    clientId,
                    writer,
                    NetworkDelivery.Reliable);
            }
        }

        public void SendSimHashToServer(OnlineSimHashPayload payload) {
            if (networkManager == null || !networkManager.IsClient || networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null) {
                return;
            }

            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteNetworkSerializable(payload);
            customMessaging.SendNamedMessage(
                SessionConstants.MessageSimHash,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
        }

        public void SendSimResyncToClients(OnlineSimResyncPayload payload) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            var customMessaging = networkManager.CustomMessagingManager;
            if (customMessaging == null || !networkManager.IsListening) {
                return;
            }

            var localId = networkManager.LocalClientId;
            foreach (var clientId in networkManager.ConnectedClientsIds) {
                if (clientId == localId) {
                    continue;
                }

                using var writer = new FastBufferWriter(1024, Allocator.Temp, 8192);
                writer.WriteNetworkSerializable(payload);
                customMessaging.SendNamedMessage(
                    SessionConstants.MessageSimResync,
                    clientId,
                    writer,
                    NetworkDelivery.ReliableFragmentedSequenced);
            }

            Debug.Log(
                $"OnlineNetMessenger.SendSimResyncToClients: tick={payload.Tick} " +
                $"entities={payload.Entities?.Length ?? 0}");
        }

        public void BroadcastMatchSetup(MatchSetupPayload setupPayload) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(MatchSetupWriterSize, Allocator.Temp);
            writer.WriteNetworkSerializable(setupPayload);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                SessionConstants.MessageMatchSetupBroadcast,
                writer,
                NetworkDelivery.Reliable);
        }

        public void SendMatchSetupUpdateToServer(MatchSetupPayload setupPayload) {
            if (networkManager == null || !networkManager.IsConnectedClient || networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(MatchSetupWriterSize, Allocator.Temp);
            writer.WriteNetworkSerializable(setupPayload);
            networkManager.CustomMessagingManager.SendNamedMessage(
                SessionConstants.MessageMatchSetupUpdate,
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
                SessionConstants.MessagePlayerIdentity,
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
                SessionConstants.MessagePlayerIdentityRequest,
                clientId,
                writer,
                NetworkDelivery.Reliable);
        }

        public void BroadcastFlowCommand(byte command, int data = 0) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(command);
            writer.WriteValueSafe(data);
            networkManager.CustomMessagingManager.SendNamedMessageToAll(
                SessionConstants.MessageFlowCommand,
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
                SessionConstants.MessageFlowRequest,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.Reliable);
        }

        void OnInputMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadNetworkSerializable(out OnlineInputBatchPayload payload);
            if (networkManager.IsServer) {
                InputReceived?.Invoke(senderClientId, payload);
                return;
            }

            HostInputReceived?.Invoke(payload);
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

        void OnDiceMotionMessage(ulong senderClientId, FastBufferReader reader) {
            if (networkManager != null && networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineDiceMotionEvent motionEvent);
            Debug.Log(
                $"OnlineNetMessenger.OnDiceMotionMessage: entity={motionEvent.EntityId} " +
                $"kind={motionEvent.Kind} catalogSide={motionEvent.CatalogSide}");
            DiceMotionReceived?.Invoke(motionEvent);
        }

        void OnAttackQueueMessage(ulong senderClientId, FastBufferReader reader) {
            if (networkManager != null && networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineAttackQueueSnapshot queueSnapshot);
            Debug.Log(
                $"OnlineNetMessenger.OnAttackQueueMessage: " +
                $"p1={queueSnapshot.Player1Volleys?.Length ?? 0} p2={queueSnapshot.Player2Volleys?.Length ?? 0}");
            AttackQueueReceived?.Invoke(queueSnapshot);
        }

        void OnDiceSpawnMessage(ulong senderClientId, FastBufferReader reader) {
            if (networkManager != null && networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineDiceSpawnCommand command);
            Debug.Log(
                $"OnlineNetMessenger.OnDiceSpawnMessage: reason={command.Reason} kind={command.Kind} " +
                $"cell=({command.GridX},{command.GridY})");
            DiceSpawnReceived?.Invoke(command);
        }

        void OnCharacterStateMessage(ulong senderClientId, FastBufferReader reader) {
            if (networkManager != null && networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineCharacterStateBatch batch);
            CharacterStateReceived?.Invoke(batch);
        }

        void OnMatchStartMessage(ulong senderClientId, FastBufferReader reader) {
            // Listen-server must not treat its own MatchStart as a client presentation start.
            if (networkManager != null && networkManager.IsServer) {
                return;
            }

            Debug.Log(
                $"OnlineNetMessenger.OnMatchStartMessage received: sender={senderClientId} " +
                $"subscribers={MatchStartReceived?.GetInvocationList().Length ?? 0}");
            try {
                reader.ReadNetworkSerializable(out MatchSetupPayload setupPayload);
                reader.ReadValueSafe(out int matchSeed);
                if (matchSeed != 0) {
                    setupPayload.MatchSeed = matchSeed;
                }

                Debug.Log(
                    $"OnlineNetMessenger.OnMatchStartMessage: deserialized seed={setupPayload.MatchSeed}");
                MatchSetupReceived?.Invoke(setupPayload);
                MatchStartReceived?.Invoke();
            } catch (System.Exception ex) {
                Debug.LogError($"OnlineNetMessenger.OnMatchStartMessage: failed: {ex}");
            }
        }

        void OnMatchStartAckMessage(ulong senderClientId, FastBufferReader reader) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            reader.ReadValueSafe(out byte _);
            Debug.Log($"OnlineNetMessenger.OnMatchStartAckMessage: from clientId={senderClientId}");
            MatchStartAckReceived?.Invoke(senderClientId);
        }

        void OnLockstepReadyMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadValueSafe(out byte _);
            if (networkManager.IsServer) {
                LockstepReadyFromClient?.Invoke(senderClientId);
                return;
            }

            LockstepReadyReceived?.Invoke();
        }

        void OnSimHashMessage(ulong senderClientId, FastBufferReader reader) {
            if (networkManager == null || !networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineSimHashPayload payload);
            SimHashReceived?.Invoke(payload);
        }

        void OnSimResyncMessage(ulong senderClientId, FastBufferReader reader) {
            if (networkManager == null || networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out OnlineSimResyncPayload payload);
            SimResyncReceived?.Invoke(payload);
        }

        void OnMatchSetupBroadcastMessage(ulong senderClientId, FastBufferReader reader) {
            reader.ReadNetworkSerializable(out MatchSetupPayload setupPayload);
            MatchSetupBroadcastReceived?.Invoke(setupPayload);
        }

        void OnMatchSetupUpdateMessage(ulong senderClientId, FastBufferReader reader) {
            if (!networkManager.IsServer) {
                return;
            }

            reader.ReadNetworkSerializable(out MatchSetupPayload setupPayload);
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
            reader.ReadValueSafe(out int data);
            FlowCommandReceived?.Invoke(command, data);
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
