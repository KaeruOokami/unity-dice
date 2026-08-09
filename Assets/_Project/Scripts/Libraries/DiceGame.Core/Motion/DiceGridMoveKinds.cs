namespace DiceGame.Core
{
    /// <summary>
    /// Copied from production <c>GhostLandingMode</c> (noEngine home for Quantum + Unity).
    /// </summary>
    public enum GhostLandingMode
    {
        None,
        CellSwap,
        InCellPromoteGhost
    }

    public enum DiceGridMoveKind
    {
        Parallel,
        Stack,
        Demote
    }
}
