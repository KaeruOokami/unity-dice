using DiceGame.Config;
using DiceGame.Gameplay.Character;
using Unity.Netcode;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Host-authoritative character pose for character-only rollback (not board/dice).
    /// </summary>
    public struct OnlineCharacterStatePayload : INetworkSerializable
    {
        public uint Sequence;
        public byte Slot;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float RotY;
        public float Speed;
        public byte Flags;

        public const byte FlagBusy = 1 << 0;

        public PlayerSlot PlayerSlot => (PlayerSlot)Slot;
        public Vector3 Position => new(PosX, PosY, PosZ);
        public bool IsBusy => (Flags & FlagBusy) != 0;

        public Quaternion Rotation => Quaternion.Euler(0f, RotY, 0f);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref PosX);
            serializer.SerializeValue(ref PosY);
            serializer.SerializeValue(ref PosZ);
            serializer.SerializeValue(ref RotY);
            serializer.SerializeValue(ref Speed);
            serializer.SerializeValue(ref Flags);
        }

        public static OnlineCharacterStatePayload FromCharacter(
            uint sequence,
            GameCharacterController character) {
            var state = character.CaptureRollbackState(sequence);
            return new OnlineCharacterStatePayload {
                Sequence = state.Sequence,
                Slot = (byte)character.PlayerSlot,
                PosX = state.Position.x,
                PosY = state.Position.y,
                PosZ = state.Position.z,
                RotY = state.Rotation.eulerAngles.y,
                Speed = state.Speed,
                Flags = state.IsBusy ? FlagBusy : (byte)0
            };
        }
    }

    public struct OnlineCharacterStateBatch : INetworkSerializable
    {
        public OnlineCharacterStatePayload[] States;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            var count = States?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                States = count > 0
                    ? new OnlineCharacterStatePayload[count]
                    : System.Array.Empty<OnlineCharacterStatePayload>();
            }

            for (var i = 0; i < count; i++) {
                var state = States[i];
                state.NetworkSerialize(serializer);
                States[i] = state;
            }
        }
    }
}
