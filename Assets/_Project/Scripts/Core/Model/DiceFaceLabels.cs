namespace DiceGame.Core
{
    /// <summary>
    /// Global pip-label reverse switch. Physical <see cref="DiceOrientation"/> stays unchanged;
    /// match/facing reads go through <see cref="ResolveMatchFace"/>, and views invert the mesh.
    /// </summary>
    public static class DiceFaceLabels
    {
        public static bool Reversed { get; set; }

        public static int ResolveMatchFace(int topFace) {
            return Reversed ? DiceOrientation.OppositeFace(topFace) : topFace;
        }
    }
}
