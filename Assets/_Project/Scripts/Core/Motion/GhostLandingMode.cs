namespace DiceGame.Core
{
    public enum GhostLandingMode
    {
        None,
        /// <summary>Mover takes ghost cell; ghost takes mover's previous cell (same tiers). Horizontal overlap only.</summary>
        CellSwap,
        /// <summary>
        /// Same cell after vertical fall onto ghost Bottom: mover becomes Bottom, ghost becomes Top.
        /// </summary>
        InCellPromoteGhost
    }
}
