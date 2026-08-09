namespace DiceGame.SimShared.Move
{
    /// <summary>
    /// Copied from production <c>DiceGame.Placement.MoveAction</c>.
    /// </summary>
    public enum MoveAction
    {
        Blocked = 0,
        ContinueToLanding = 1,
        ExpandedFootprintWalk,
        PlayerWalk,
        PlayerWalkFloor,
        FloorToBottomMount,
        TierLanding,
        IceSlide,
        CoupledJumpGrid,
        TopFall,
        GridRoll,
        HeightTransfer,
    }
}
