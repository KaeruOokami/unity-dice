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

        public bool ChangesTier => From.Tier != To.Tier;
    }
}
