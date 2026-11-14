namespace DiceGame.Core
{
    /// <summary>
    /// Session modifier for reverse mode. Applied after Kind <c>ResolveMatchFace</c>
    /// (see <see cref="DiceGame.Gameplay.DiceController.MatchFace"/>).
    /// Physical <see cref="DiceOrientation"/> stays unchanged; views invert the mesh when active.
    /// </summary>
    public static class DiceFaceLabels
    {
        public static bool Reversed { get; set; }

        public static int ResolveMatchFace(int kindMatchFace) {
            return Reversed ? DiceOrientation.OppositeFace(kindMatchFace) : kindMatchFace;
        }
    }
}
