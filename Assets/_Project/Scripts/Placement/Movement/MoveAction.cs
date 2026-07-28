namespace DiceGame.Placement
{
    /// <summary>
    /// Single permitted movement intent for a cell transition.
    /// Selected from <see cref="MoveFacts"/> by <see cref="MoveActionSelector"/>;
    /// builders must not fall through to a different action on failure.
    /// </summary>
    public enum MoveAction
    {
        /// <summary>No legal transition.</summary>
        Blocked = 0,

        /// <summary>Internal: coupled probes miss; landing table owns the cell.</summary>
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
