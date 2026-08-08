namespace Quantum
{
    /// <summary>
    /// Quantum-pure subset of <c>DiceGame.Core.DiceCapabilities</c> for Quantum simulation rules.
    /// Ice slide / Magnet chain / Jumbo footprint remain deferred (flags only).
    /// </summary>
    public readonly struct DiceKindCapabilities
    {
        public bool CanBeLiftedByPlayer { get; }
        public bool CanBePushedByPlayer { get; }
        public bool IsPlayerPassThrough { get; }
        public bool AllowsDiceSwapThrough { get; }
        public bool SlideUntilBlocked { get; }
        public bool HasMagnetCoupling { get; }
        public bool HasExpandedFootprint { get; }

        public DiceKindCapabilities(
            bool canBeLiftedByPlayer,
            bool canBePushedByPlayer,
            bool isPlayerPassThrough = false,
            bool allowsDiceSwapThrough = false,
            bool slideUntilBlocked = false,
            bool hasMagnetCoupling = false,
            bool hasExpandedFootprint = false)
        {
            CanBeLiftedByPlayer = canBeLiftedByPlayer;
            CanBePushedByPlayer = canBePushedByPlayer;
            IsPlayerPassThrough = isPlayerPassThrough;
            AllowsDiceSwapThrough = allowsDiceSwapThrough;
            SlideUntilBlocked = slideUntilBlocked;
            HasMagnetCoupling = hasMagnetCoupling;
            HasExpandedFootprint = hasExpandedFootprint;
        }

        public static DiceKindCapabilities For(DiceKind kind)
        {
            switch (kind)
            {
                case DiceKind.Iron:
                case DiceKind.Stone:
                    return new DiceKindCapabilities(
                        canBeLiftedByPlayer: false,
                        canBePushedByPlayer: false);
                case DiceKind.Jumbo:
                    return new DiceKindCapabilities(
                        canBeLiftedByPlayer: false,
                        canBePushedByPlayer: false,
                        hasExpandedFootprint: true);
                case DiceKind.Ghost:
                    return new DiceKindCapabilities(
                        canBeLiftedByPlayer: false,
                        canBePushedByPlayer: false,
                        isPlayerPassThrough: true,
                        allowsDiceSwapThrough: true);
                case DiceKind.Ice:
                    return new DiceKindCapabilities(
                        canBeLiftedByPlayer: true,
                        canBePushedByPlayer: true,
                        slideUntilBlocked: true);
                case DiceKind.Magnet:
                    return new DiceKindCapabilities(
                        canBeLiftedByPlayer: true,
                        canBePushedByPlayer: true,
                        hasMagnetCoupling: true);
                default:
                    return new DiceKindCapabilities(
                        canBeLiftedByPlayer: true,
                        canBePushedByPlayer: true);
            }
        }
    }
}
