using UnityEngine;

namespace DiceGame.Core
{
    public enum DiceGridMoveKind
    {
        Parallel,
        Stack,
        Demote
    }

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
