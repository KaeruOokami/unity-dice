using System;
using DiceGame.Config;
using DiceGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Authoritative entity summary. Always sent as a single Reliable datagram.
    /// Dice: logical board state only (no world pose chase).
    /// Character: world pose.
    /// </summary>
    public struct OnlineTransformSnapshot : INetworkSerializable
    {
        public uint Id;
        public byte Flags;
        public byte Kind;
        public byte CatalogSide;
        public short GridX;
        public short GridY;
        public byte Tier;
        public byte TopFace;
        public byte NorthFace;
        public byte EastFace;
        public Vector3 Position;
        public Quaternion Rotation;

        public const byte FlagCharacter = 1 << 0;
        public const byte FlagDice = 1 << 1;
        public const byte FlagActive = 1 << 2;

        public bool IsCharacter => (Flags & FlagCharacter) != 0;
        public bool IsDice => (Flags & FlagDice) != 0;
        public bool IsActive => (Flags & FlagActive) != 0;

        public PlayerSlot CatalogPlayerSlot => (PlayerSlot)CatalogSide;

        public DiceState ToDiceState() {
            return new DiceState(
                new Vector2Int(GridX, GridY),
                new DiceOrientation(TopFace, NorthFace, EastFace),
                (DiceStackTier)Tier,
                (DiceKind)Kind);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref Kind);

            if ((Flags & FlagDice) != 0) {
                serializer.SerializeValue(ref CatalogSide);
                serializer.SerializeValue(ref GridX);
                serializer.SerializeValue(ref GridY);
                serializer.SerializeValue(ref Tier);
                serializer.SerializeValue(ref TopFace);
                serializer.SerializeValue(ref NorthFace);
                serializer.SerializeValue(ref EastFace);
                if (serializer.IsReader) {
                    Position = default;
                    Rotation = Quaternion.identity;
                }
            } else {
                if (serializer.IsWriter) {
                    WriteHalf(serializer, Position.x);
                    WriteHalf(serializer, Position.y);
                    WriteHalf(serializer, Position.z);
                    var rot = Rotation.normalized;
                    WriteHalf(serializer, rot.x);
                    WriteHalf(serializer, rot.y);
                    WriteHalf(serializer, rot.z);
                    WriteHalf(serializer, rot.w);
                } else {
                    Position = new Vector3(
                        ReadHalf(serializer),
                        ReadHalf(serializer),
                        ReadHalf(serializer));
                    Rotation = new Quaternion(
                        ReadHalf(serializer),
                        ReadHalf(serializer),
                        ReadHalf(serializer),
                        ReadHalf(serializer)).normalized;
                    CatalogSide = 0;
                    GridX = 0;
                    GridY = 0;
                    Tier = 0;
                    TopFace = 0;
                    NorthFace = 0;
                    EastFace = 0;
                }
            }
        }

        static void WriteHalf<T>(BufferSerializer<T> serializer, float value) where T : IReaderWriter {
            var bits = Mathf.FloatToHalf(value);
            serializer.SerializeValue(ref bits);
        }

        static float ReadHalf<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            ushort bits = 0;
            serializer.SerializeValue(ref bits);
            return Mathf.HalfToFloat(bits);
        }
    }

    /// <summary>
    /// Single Reliable board snapshot (ChunkCount is always 1).
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
