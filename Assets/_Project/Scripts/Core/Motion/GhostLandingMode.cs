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
        InCellPromoteGhost,
        /// <summary>
        /// Top entry into a cell whose Bottom and Top are both swap-through ghosts.
        /// Final occupancy: mover Bottom, former Bottom ghost → Top, former Top ghost → mover's
        /// previous cell (same tier). <see cref="DiceState"/> ghostFrom/To describe the exiting Top ghost;
        /// Bottom promote is derived at apply time from the target cell.
        /// </summary>
        CellSwapWithInCellPromote
    }
}
