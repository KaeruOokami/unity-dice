namespace DiceGame.SimShared.Motion
{
    using DiceGame.Core;

    /// <summary>
    /// Copied from production <c>DiceGame.Core.DiceState</c> with int grid (no UnityEngine).
    /// </summary>
    public struct DiceState
    {
        public int GridX;
        public int GridY;
        public DiceOrientation Orientation;
        public DiceStackTier Tier;
        public DiceKind Kind;

        public DiceState(
            int gridX,
            int gridY,
            DiceOrientation orientation,
            DiceStackTier tier = DiceStackTier.Bottom,
            DiceKind kind = DiceKind.Normal)
        {
            GridX = gridX;
            GridY = gridY;
            Orientation = orientation;
            Tier = tier;
            Kind = kind;
        }
    }

    /// <summary>
    /// Copied from production <c>DiceGame.Core.DiceGridMovePlan</c>.
    /// </summary>
    public struct DiceGridMovePlan
    {
        public DiceState From;
        public DiceState To;
        public DiceGridMoveKind Kind;
        public Direction Direction;
        public int Distance;
        public GhostLandingMode GhostLanding;
        public DiceState GhostFrom;
        public DiceState GhostTo;

        public bool ChangesTier => From.Tier != To.Tier;
        public bool HasGhostSwap => GhostLanding != GhostLandingMode.None;
    }
}
