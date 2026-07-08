using DiceGame.Gameplay;

namespace DiceGame.Placement.Support
{
    public readonly struct SupportRef
    {
        public SupportKind Kind { get; }
        public DiceController Dice { get; }
        public DiceSurfaceLevel DiceSurfaceLevel { get; }

        SupportRef(SupportKind kind, DiceController dice, DiceSurfaceLevel diceSurfaceLevel) {
            Kind = kind;
            Dice = dice;
            DiceSurfaceLevel = diceSurfaceLevel;
        }

        public static SupportRef None() => new SupportRef(SupportKind.None, null, DiceSurfaceLevel.Bottom);

        public static SupportRef Floor() => new SupportRef(SupportKind.Floor, null, DiceSurfaceLevel.Bottom);

        public static SupportRef DiceSupport(DiceController dice, DiceSurfaceLevel surfaceLevel) {
            return new SupportRef(SupportKind.Dice, dice, surfaceLevel);
        }
    }
}

