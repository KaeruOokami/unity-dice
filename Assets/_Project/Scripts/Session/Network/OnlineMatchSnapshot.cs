using System;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    public struct OnlineTransformSnapshot : INetworkSerializable
    {
        public uint Id;
        public Vector3 Position;
        public Quaternion Rotation;
        public byte Kind;
        public byte Flags;

        public const byte FlagCharacter = 1 << 0;
        public const byte FlagDice = 1 << 1;
        public const byte FlagActive = 1 << 2;

        public bool IsCharacter => (Flags & FlagCharacter) != 0;
        public bool IsDice => (Flags & FlagDice) != 0;
        public bool IsActive => (Flags & FlagActive) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Flags);
        }
    }

    public struct OnlineMatchSnapshot : INetworkSerializable, IEquatable<OnlineMatchSnapshot>
    {
        public OnlineTransformSnapshot[] Entities;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            var count = Entities?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                Entities = new OnlineTransformSnapshot[count];
            }

            for (var i = 0; i < count; i++) {
                serializer.SerializeValue(ref Entities[i]);
            }
        }

        public bool Equals(OnlineMatchSnapshot other) {
            return ReferenceEquals(Entities, other.Entities);
        }
    }
}
