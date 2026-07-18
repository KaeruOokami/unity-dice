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
        }

        public static OnlineInputPayload FromSource(
            Vector2 move,
            bool lift,
            bool jump,
            bool hasDirection,
            Direction direction) {
            return new OnlineInputPayload {
                MoveX = move.x,
                MoveY = move.y,
                LiftPressed = lift,
                JumpPressed = jump,
                HasDirection = hasDirection,
                DirectionValue = (byte)direction
            };
        }
    }
}
