namespace DiceGame.Core
{
    public enum GhostLandingMode
    {
        None,
        /// <summary>
        /// Mover takes ghost cell/slot; ghost is displaced.
        /// Same-tier: previous cell same tier. Ascent onto Top ghost: previous cell as Bottom.
        /// </summary>
        CellSwap,
        /// <summary>
        /// Descent onto ghost Bottom after fall: same cell, mover Bottom, ghost Top.
        /// </summary>
        InCellPromoteGhost
    }
}
