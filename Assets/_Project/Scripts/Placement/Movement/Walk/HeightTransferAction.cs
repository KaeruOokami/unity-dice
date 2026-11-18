namespace DiceGame.Placement
{
    /// <summary>
    /// Permitted height-transfer intent after <see cref="MoveAction.HeightTransfer"/> is selected.
    /// </summary>
    public enum HeightTransferAction
    {
        Blocked = 0,
        SameTierTransfer,
        /// <summary>Walk onto a lower-tier die when step height allows (same as Sink when gap is small).</summary>
        LowerLevelWalk,
        DissolveDescentHold,
        LowerLevelPlayerOnlyJump,
    }
}
