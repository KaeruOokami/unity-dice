using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public struct CharacterRollbackState
    {
        public uint Sequence;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Speed;
        public bool IsBusy;
    }
}
