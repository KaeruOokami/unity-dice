using DiceGame.Core;
using DiceGame.Placement.Support;

namespace DiceGame.Placement
{
    /// <summary>
    /// Normalized surface height levels for board surfaces and movement transitions.
    /// 0=floor, 1=bottom dice surface, 2=top dice surface (including stack-top on bottom dice).
    /// </summary>
    public static class SurfaceHeightLevel
    {
        public const int Floor = 0;
        public const int Bottom = 1;
        public const int Top = 2;

        public static int FromDiceStackTier(DiceStackTier tier) =>
            tier == DiceStackTier.Top ? Top : Bottom;

        public static DiceStackTier ToDiceStackTier(int level) =>
            level >= Top ? DiceStackTier.Top : DiceStackTier.Bottom;

        public static DiceSurfaceLevel ToDiceSurfaceLevel(int level) =>
            level >= Top ? DiceSurfaceLevel.Top : DiceSurfaceLevel.Bottom;

        public static bool IsFloor(int level) => level <= Floor;

        public static bool IsAtOrAboveTop(int level) => level >= Top;
    }
}
