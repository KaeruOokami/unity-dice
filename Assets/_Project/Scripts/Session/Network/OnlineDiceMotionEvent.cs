using DiceGame.Config;
using DiceGame.Core;
using DiceGame.View;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    public struct OnlineDiceMotionEvent : INetworkSerializable
    {
        public uint EntityId;
        public byte Kind;
        public byte Direction;
        public short FromGridX;
        public short FromGridY;
        public byte FromTop;
        public byte FromNorth;
        public byte FromEast;
        public byte FromTier;
        public byte FromKind;
        public short ToGridX;
        public short ToGridY;
        public byte ToTop;
        public byte ToNorth;
        public byte ToEast;
        public byte ToTier;
        public byte ToKind;
        public float JumpYOffset;
        public byte RollDistance;
        public byte Flags;
        public float FromSurfaceWorldY;
        public float ToSurfaceWorldY;
        public byte TransitionPath;
        public byte SlideCellDistance;
        public float FallGravityScale;
        public byte ErasureKind;
        public byte TopFace;
        public Color32 EmissionColor;
        public Vector3 FromWorldOverride;
        public Vector3 ToWorldOverride;
        public byte CatalogSide;

        public const byte FlagFallBeforeSnap = 1 << 0;
        public const byte FlagUseArcJump = 1 << 1;
        public const byte FlagEnableSpawnBounce = 1 << 2;
        public const byte FlagHasEmissionOverride = 1 << 3;
        public const byte FlagHasFromWorldOverride = 1 << 4;
        public const byte FlagHasToWorldOverride = 1 << 5;
        public const byte FlagSnapToGridOnComplete = 1 << 6;

        public static OnlineDiceMotionEvent FromRequest(
            uint entityId,
            DiceVisualMotionRequest request,
            PlayerSlot catalogSide) {
            return new OnlineDiceMotionEvent {
                EntityId = entityId,
                Kind = (byte)request.Kind,
                Direction = (byte)request.Direction,
                FromGridX = (short)request.FromState.GridPos.x,
                FromGridY = (short)request.FromState.GridPos.y,
                FromTop = (byte)request.FromState.Orientation.Top,
                FromNorth = (byte)request.FromState.Orientation.North,
                FromEast = (byte)request.FromState.Orientation.East,
                FromTier = (byte)request.FromState.Tier,
                FromKind = (byte)request.FromState.Kind,
                ToGridX = (short)request.ToState.GridPos.x,
                ToGridY = (short)request.ToState.GridPos.y,
                ToTop = (byte)request.ToState.Orientation.Top,
                ToNorth = (byte)request.ToState.Orientation.North,
                ToEast = (byte)request.ToState.Orientation.East,
                ToTier = (byte)request.ToState.Tier,
                ToKind = (byte)request.ToState.Kind,
                JumpYOffset = request.JumpYOffset,
                RollDistance = (byte)Mathf.Clamp(request.RollDistance, 0, 255),
                Flags = BuildFlags(request),
                FromSurfaceWorldY = request.FromSurfaceWorldY,
                ToSurfaceWorldY = request.ToSurfaceWorldY,
                TransitionPath = (byte)request.TransitionPath,
                SlideCellDistance = (byte)Mathf.Clamp(request.SlideCellDistance, 0, 255),
                FallGravityScale = request.FallGravityScale,
                ErasureKind = (byte)request.ErasureKind,
                TopFace = (byte)Mathf.Clamp(request.TopFace, 0, 255),
                EmissionColor = request.HasEmissionOverride
                    ? (Color32)request.EmissionColor
                    : default,
                FromWorldOverride = request.FromWorldOverride,
                ToWorldOverride = request.ToWorldOverride,
                CatalogSide = (byte)catalogSide
            };
        }

        static byte BuildFlags(DiceVisualMotionRequest request) {
            byte flags = 0;
            if (request.FallBeforeSnap) {
                flags |= FlagFallBeforeSnap;
            }

            if (request.UseArcJump) {
                flags |= FlagUseArcJump;
            }

            if (request.EnableSpawnBounce) {
                flags |= FlagEnableSpawnBounce;
            }

            if (request.HasEmissionOverride) {
                flags |= FlagHasEmissionOverride;
            }

            if (request.HasFromWorldOverride) {
                flags |= FlagHasFromWorldOverride;
            }

            if (request.HasToWorldOverride) {
                flags |= FlagHasToWorldOverride;
            }

            if (request.SnapToGridOnComplete) {
                flags |= FlagSnapToGridOnComplete;
            }

            return flags;
        }

        public DiceVisualMotionKind MotionKind => (DiceVisualMotionKind)Kind;

        public DiceState FromState => new(
            new Vector2Int(FromGridX, FromGridY),
            new DiceOrientation(FromTop, FromNorth, FromEast),
            (DiceStackTier)FromTier,
            (DiceKind)FromKind);

        public DiceState ToState => new(
            new Vector2Int(ToGridX, ToGridY),
            new DiceOrientation(ToTop, ToNorth, ToEast),
            (DiceStackTier)ToTier,
            (DiceKind)ToKind);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref EntityId);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref FromGridX);
            serializer.SerializeValue(ref FromGridY);
            serializer.SerializeValue(ref FromTop);
            serializer.SerializeValue(ref FromNorth);
            serializer.SerializeValue(ref FromEast);
            serializer.SerializeValue(ref FromTier);
            serializer.SerializeValue(ref FromKind);
            serializer.SerializeValue(ref ToGridX);
            serializer.SerializeValue(ref ToGridY);
            serializer.SerializeValue(ref ToTop);
            serializer.SerializeValue(ref ToNorth);
            serializer.SerializeValue(ref ToEast);
            serializer.SerializeValue(ref ToTier);
            serializer.SerializeValue(ref ToKind);
            serializer.SerializeValue(ref JumpYOffset);
            serializer.SerializeValue(ref RollDistance);
            serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref FromSurfaceWorldY);
            serializer.SerializeValue(ref ToSurfaceWorldY);
            serializer.SerializeValue(ref TransitionPath);
            serializer.SerializeValue(ref SlideCellDistance);
            serializer.SerializeValue(ref FallGravityScale);
            serializer.SerializeValue(ref ErasureKind);
            serializer.SerializeValue(ref TopFace);
            if ((Flags & FlagHasEmissionOverride) != 0) {
                serializer.SerializeValue(ref EmissionColor);
            } else if (serializer.IsReader) {
                EmissionColor = default;
            }

            if ((Flags & FlagHasFromWorldOverride) != 0) {
                serializer.SerializeValue(ref FromWorldOverride);
            } else if (serializer.IsReader) {
                FromWorldOverride = default;
            }

            if ((Flags & FlagHasToWorldOverride) != 0) {
                serializer.SerializeValue(ref ToWorldOverride);
            } else if (serializer.IsReader) {
                ToWorldOverride = default;
            }

            serializer.SerializeValue(ref CatalogSide);
        }
    }
}
