namespace DiceGame.Core
{
    public enum GhostLandingMode
    {
        None,
        /// <summary>Mover takes ghost cell; ghost takes mover's previous cell (same tiers).</summary>
        CellSwap,
        /// <summary>Same cell: mover becomes Bottom, ghost becomes Top.</summary>
        InCellPromoteGhost
    }
}
