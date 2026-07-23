using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.View
{
    public enum DiceVisualMotionKind : byte
    {
        None = 0,
        JumpRoll = 1,
        Transition = 2,
        SpawnFall = 3,
        SpawnEmerge = 4,
        Erasure = 5,
        OneVanish = 6
    }

    public struct DiceVisualMotionRequest
    {
        public DiceVisualMotionKind Kind;
        public Direction Direction;
        public DiceState FromState;
        public DiceState ToState;
        public float JumpYOffset;
        public int RollDistance;
        public bool FallBeforeSnap;
        public bool UseArcJump;
        public float FromSurfaceWorldY;
        public float ToSurfaceWorldY;
        public DiceTransitionPath TransitionPath;
        public int SlideCellDistance;
        public bool HasFromWorldOverride;
        public Vector3 FromWorldOverride;
        public bool HasToWorldOverride;
        public Vector3 ToWorldOverride;
        public bool SnapToGridOnComplete;
        public bool EnableSpawnBounce;
        public float FallGravityScale;
        public ErasureKind ErasureKind;
        public int TopFace;
        public bool HasEmissionOverride;
        public Color EmissionColor;
    }

    /// <summary>
    /// Host presentation emits motion starts here; Session.Network listens without View→Network coupling.
    /// </summary>
    public static class DiceVisualMotionHub
    {
        public static event Action<DiceView, DiceVisualMotionRequest> MotionStarted;

        public static void Raise(DiceView view, DiceVisualMotionRequest request) {
            if (view == null || request.Kind == DiceVisualMotionKind.None) {
                return;
            }

            MotionStarted?.Invoke(view, request);
        }
    }
}
