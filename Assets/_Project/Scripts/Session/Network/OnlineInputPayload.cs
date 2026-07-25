using System;
using DiceGame.Core;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    public struct OnlineInputPayload : INetworkSerializable
    {
        public float MoveX;
        public float MoveY;
        public bool LiftPressed;
        public bool JumpPressed;
        public bool HasDirection;
        public byte DirectionValue;
        public uint Sequence;
        public uint Tick;

        public Vector2 Move => new(MoveX, MoveY);

        public bool TryGetDirection(out Direction direction) {
            direction = (Direction)DirectionValue;
            return HasDirection;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref MoveX);
            serializer.SerializeValue(ref MoveY);
            serializer.SerializeValue(ref LiftPressed);
            serializer.SerializeValue(ref JumpPressed);
            serializer.SerializeValue(ref HasDirection);
            serializer.SerializeValue(ref DirectionValue);
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Tick);
        }

        public static OnlineInputPayload FromSource(
            Vector2 move,
            bool lift,
            bool jump,
            bool hasDirection,
            Direction direction,
            uint sequence = 0,
            uint tick = 0) {
            return new OnlineInputPayload {
                MoveX = move.x,
                MoveY = move.y,
                LiftPressed = lift,
                JumpPressed = jump,
                HasDirection = hasDirection,
                DirectionValue = (byte)direction,
                Sequence = sequence,
                Tick = tick
            };
        }
    }

    /// <summary>
    /// A window of per-tick inputs sent together for redundancy: a single lost
    /// packet is recovered by the next batch instead of waiting for an explicit
    /// resend, without changing the reliable-sequenced transport.
    /// </summary>
    public struct OnlineInputBatchPayload : INetworkSerializable
    {
        public OnlineInputPayload[] Inputs;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            var count = Inputs?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                Inputs = count > 0
                    ? new OnlineInputPayload[count]
                    : Array.Empty<OnlineInputPayload>();
            }

            for (var i = 0; i < count; i++) {
                var input = Inputs[i];
                input.NetworkSerialize(serializer);
                Inputs[i] = input;
            }
        }
    }
}
