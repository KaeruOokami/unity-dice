using System;
using DiceGame.Core;

namespace DiceGame.Gameplay
{
    public struct DiceMoveVisualContext
    {
        public bool IsJump;
        public float JumpYOffset;
        public Func<VerticalMotionState> JumpMotionProvider;

        public static DiceMoveVisualContext Ground => new DiceMoveVisualContext {
            IsJump = false,
            JumpYOffset = 0f,
            JumpMotionProvider = null
        };

        public static DiceMoveVisualContext Jump(
            float jumpYOffset,
            Func<VerticalMotionState> jumpMotionProvider = null) {
            return new DiceMoveVisualContext {
                IsJump = true,
                JumpYOffset = jumpYOffset,
                JumpMotionProvider = jumpMotionProvider
            };
        }
    }
}
