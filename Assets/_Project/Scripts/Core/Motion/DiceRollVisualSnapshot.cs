using UnityEngine;

namespace DiceGame.Core
{
    public struct DiceRollVisualSnapshot
    {
        public Vector3 WorldPosition;
        public Quaternion Rotation;
        public bool IsValid;

        public static DiceRollVisualSnapshot Invalid => default;
    }
}
