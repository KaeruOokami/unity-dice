using System;
using Unity.Netcode;

namespace DiceGame.Session.Network
{
    public struct OnlineSimHashPayload : INetworkSerializable
    {
        public uint Tick;
        public uint Hash;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Hash);
        }
    }

    /// <summary>
    /// Host-authoritative board+character dump used to realign a desynced client.
    /// </summary>
    public struct OnlineSimResyncPayload : INetworkSerializable
    {
        public uint Tick;
        public OnlineTransformSnapshot[] Entities;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Tick);
            var count = Entities?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                Entities = count > 0
                    ? new OnlineTransformSnapshot[count]
                    : Array.Empty<OnlineTransformSnapshot>();
            }

            for (var i = 0; i < count; i++) {
                var entity = Entities[i];
                entity.NetworkSerialize(serializer);
                Entities[i] = entity;
            }
        }
    }
}
