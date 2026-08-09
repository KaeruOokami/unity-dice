using UnityEngine;

namespace DiceGame.Core
{
    /// <summary>
    /// Production plan type (Unity <see cref="DiceState"/>). Enums live in noEngine Core library.
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
