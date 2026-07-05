namespace DiceGame.Core
{
    public readonly struct DiceCapabilities
    {
        public bool CanBePushedByPlayer { get; }
        public bool CanBeLiftedByPlayer { get; }
        public bool PushUsesRoll { get; }
        public bool CanGridRoll { get; }
        public bool SlideUntilBlocked { get; }
        public bool HasMagnetCoupling { get; }
        public bool HasSpawnBounce { get; }
        public float RollDurationMultiplier { get; }

        public DiceCapabilities(
            bool canBePushedByPlayer,
            bool canBeLiftedByPlayer,
            bool pushUsesRoll,
            bool canGridRoll,
            bool slideUntilBlocked,
            bool hasMagnetCoupling,
            bool hasSpawnBounce,
            float rollDurationMultiplier) {
            CanBePushedByPlayer = canBePushedByPlayer;
            CanBeLiftedByPlayer = canBeLiftedByPlayer;
            PushUsesRoll = pushUsesRoll;
            CanGridRoll = canGridRoll;
            SlideUntilBlocked = slideUntilBlocked;
            HasMagnetCoupling = hasMagnetCoupling;
            HasSpawnBounce = hasSpawnBounce;
            RollDurationMultiplier = rollDurationMultiplier;
        }
    }
}
