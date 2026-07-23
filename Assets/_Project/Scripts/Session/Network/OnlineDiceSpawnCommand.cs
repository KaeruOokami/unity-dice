using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using Unity.Netcode;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Host-authoritative dice spawn for the full-sim online experiment.
    /// Client applies this instead of rolling local RNG for continuous/attack/jumbo spawns.
    /// </summary>
    public struct OnlineDiceSpawnCommand : INetworkSerializable
    {
        public const byte ReasonContinuous = 1;
        public const byte ReasonAttack = 2;
        public const byte ReasonJumbo = 3;

        public byte Reason;
        public byte Kind;
        public byte OwnerSlot;
        public short GridX;
        public short GridY;
        public byte Tier;
        public byte TopFace;
        public byte NorthFace;
        public byte EastFace;
        public byte UseSpawnAppear;
        public byte ForceFallFromAbove;

        public PlayerSlot Owner => (PlayerSlot)OwnerSlot;

        public DiceState ToDiceState() {
            return new DiceState(
                new UnityEngine.Vector2Int(GridX, GridY),
                new DiceOrientation(TopFace, NorthFace, EastFace),
                (DiceStackTier)Tier,
                (DiceKind)Kind);
        }

        public static OnlineDiceSpawnCommand FromDice(
            DiceController dice,
            byte reason,
            bool useSpawnAppear,
            bool forceFallFromAbove) {
            var state = dice.CurrentState;
            return new OnlineDiceSpawnCommand {
                Reason = reason,
                Kind = (byte)state.Kind,
                OwnerSlot = 0,
                GridX = (short)state.GridPos.x,
                GridY = (short)state.GridPos.y,
                Tier = (byte)state.Tier,
                TopFace = (byte)state.Orientation.Top,
                NorthFace = (byte)state.Orientation.North,
                EastFace = (byte)state.Orientation.East,
                UseSpawnAppear = (byte)(useSpawnAppear ? 1 : 0),
                ForceFallFromAbove = (byte)(forceFallFromAbove ? 1 : 0)
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Reason);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref OwnerSlot);
            serializer.SerializeValue(ref GridX);
            serializer.SerializeValue(ref GridY);
            serializer.SerializeValue(ref Tier);
            serializer.SerializeValue(ref TopFace);
            serializer.SerializeValue(ref NorthFace);
            serializer.SerializeValue(ref EastFace);
            serializer.SerializeValue(ref UseSpawnAppear);
            serializer.SerializeValue(ref ForceFallFromAbove);
        }
    }
}
