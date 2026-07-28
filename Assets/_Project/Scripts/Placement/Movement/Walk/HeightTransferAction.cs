namespace DiceGame.Placement
{
    /// <summary>
    /// Permitted height-transfer intent after <see cref="MoveAction.HeightTransfer"/> is selected.
    /// </summary>
    public enum HeightTransferAction
    {
        Blocked = 0,
        SameTierTransfer,
        DissolveDescentHold,
        LowerLevelPlayerOnlyJump,
    }
}
