using DiceGame.Config;

namespace DiceGame.Placement
{
    public readonly struct PassabilityContext
    {
        public bool IsJumping { get; }
        public bool AllowJumpGridMove { get; }
        public bool AllowJumpTierChange { get; }
        public float FootingWorldY { get; }
        public PlayerSlot? MovementOwner { get; }

        PassabilityContext(
            bool isJumping,
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float footingWorldY,
            PlayerSlot? movementOwner) {
            IsJumping = isJumping;
            AllowJumpGridMove = allowJumpGridMove;
            AllowJumpTierChange = allowJumpTierChange;
            FootingWorldY = footingWorldY;
            MovementOwner = movementOwner;
        }

        public static PassabilityContext ForGround(float footingWorldY, PlayerSlot? movementOwner = null) {
            return new PassabilityContext(false, false, false, footingWorldY, movementOwner);
        }

        public static PassabilityContext Jump(
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float footingWorldY,
            PlayerSlot? movementOwner = null) {
            return new PassabilityContext(true, allowJumpGridMove, allowJumpTierChange, footingWorldY, movementOwner);
        }
    }
}
