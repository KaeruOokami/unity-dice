namespace Quantum
{
    using DiceGame.SimShared.Dice;

    /// <summary>
    /// Thin Quantum wrapper over shared <see cref="DiceFaceOrientation"/>.
    /// </summary>
    public static class DiceOrientation
    {
        public const int MinFace = DiceFaceOrientation.MinFace;
        public const int MaxFace = DiceFaceOrientation.MaxFace;

        public static void Default(out int top, out int north, out int east)
        {
            DiceFaceOrientation.Default(out top, out north, out east);
        }

        public static void CreateWithTopFace(int topFace, out int top, out int north, out int east)
        {
            DiceFaceOrientation.CreateWithTopFace(topFace, out top, out north, out east);
        }

        public static bool IsValid(int top, int north, int east)
        {
            return DiceFaceOrientation.IsValid(top, north, east);
        }

        public static (int top, int north, int east) RollNorth(int top, int north, int east)
        {
            DiceFaceOrientation.RollNorth(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }

        public static (int top, int north, int east) RollSouth(int top, int north, int east)
        {
            DiceFaceOrientation.RollSouth(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }

        public static (int top, int north, int east) RollEast(int top, int north, int east)
        {
            DiceFaceOrientation.RollEast(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }

        public static (int top, int north, int east) RollWest(int top, int north, int east)
        {
            DiceFaceOrientation.RollWest(top, north, east, out var t, out var n, out var e);
            return (t, n, e);
        }
    }
}
