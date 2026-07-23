using System;
using DiceGame.Core;
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
        public byte VisualKind;
        public byte TopFace;
        public float VisualProgress;
        public Color32 EmissionColor;

        public const byte FlagCharacter = 1 << 0;
        public const byte FlagDice = 1 << 1;
        public const byte FlagActive = 1 << 2;
        public const byte FlagHasEmissionOverride = 1 << 3;

        public const byte VisualNone = 0;
        public const byte VisualSink = (byte)ErasureKind.Sink;
        public const byte VisualRadiance = (byte)ErasureKind.Radiance;
        public const byte VisualOneVanish = 3;

        public bool IsCharacter => (Flags & FlagCharacter) != 0;
        public bool IsDice => (Flags & FlagDice) != 0;
        public bool IsActive => (Flags & FlagActive) != 0;
        public bool HasEmissionOverride => (Flags & FlagHasEmissionOverride) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref VisualKind);
            serializer.SerializeValue(ref TopFace);
            serializer.SerializeValue(ref VisualProgress);
            serializer.SerializeValue(ref EmissionColor);
        }
    }

    /// <summary>
    /// One MTU-safe piece of a full match snapshot. Host splits large boards across Unreliable chunks.
    /// </summary>
    public struct OnlineMatchSnapshotChunk : INetworkSerializable
    {
        public uint Sequence;
        public ushort ChunkIndex;
        public ushort ChunkCount;
        public OnlineTransformSnapshot[] Entities;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref ChunkIndex);
            serializer.SerializeValue(ref ChunkCount);

            var count = Entities?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                Entities = count > 0
                    ? new OnlineTransformSnapshot[count]
                    : Array.Empty<OnlineTransformSnapshot>();
            }

            for (var i = 0; i < count; i++) {
                // Must copy back: Entities[i] is a struct; NetworkSerialize on the indexer mutates a copy.
                var entity = Entities[i];
                entity.NetworkSerialize(serializer);
                Entities[i] = entity;
            }
        }
    }

    public struct OnlineMatchSnapshot
    {
        public OnlineTransformSnapshot[] Entities;
    }
}
