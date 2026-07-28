using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public readonly struct HeightTransferFacts
    {
        public Vector2Int FromCell { get; }
        public Vector2Int ToCell { get; }
        public int FromLevel { get; }
        public BoardSurface FromSurface { get; }
        public DiceController StandingDice { get; }
        public Direction Direction { get; }
        public bool IsJumping { get; }
        public bool AllowJumpGridMove { get; }
        public HeightReachEvaluation Reach { get; }

        public DiceController SameTierTarget { get; }
        public DiceController LowerLevelTarget { get; }
        public int LowerLevelTargetLevel { get; }

        public bool PreferCoupledGridRoll { get; }
        public bool CanSameTierTransfer { get; }
        public MovementTransition SameTierTransition { get; }
        public string SameTierRejectReason { get; }

        public bool CanDissolveDescentHold { get; }
        public MovementTransition DissolveDescentTransition { get; }

        public bool CanLowerLevelPlayerOnlyJump { get; }

        public HeightTransferFacts(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            bool isJumping,
            bool allowJumpGridMove,
            HeightReachEvaluation reach,
            DiceController sameTierTarget,
            DiceController lowerLevelTarget,
            int lowerLevelTargetLevel,
            bool preferCoupledGridRoll,
            bool canSameTierTransfer,
            MovementTransition sameTierTransition,
            string sameTierRejectReason,
            bool canDissolveDescentHold,
            MovementTransition dissolveDescentTransition,
            bool canLowerLevelPlayerOnlyJump) {
            FromCell = fromCell;
            ToCell = toCell;
            FromLevel = fromLevel;
            FromSurface = fromSurface;
            StandingDice = standingDice;
            Direction = direction;
            IsJumping = isJumping;
            AllowJumpGridMove = allowJumpGridMove;
            Reach = reach;
            SameTierTarget = sameTierTarget;
            LowerLevelTarget = lowerLevelTarget;
            LowerLevelTargetLevel = lowerLevelTargetLevel;
            PreferCoupledGridRoll = preferCoupledGridRoll;
            CanSameTierTransfer = canSameTierTransfer;
            SameTierTransition = sameTierTransition;
            SameTierRejectReason = sameTierRejectReason;
            CanDissolveDescentHold = canDissolveDescentHold;
            DissolveDescentTransition = dissolveDescentTransition;
            CanLowerLevelPlayerOnlyJump = canLowerLevelPlayerOnlyJump;
        }

        public bool HasLowerLevelFallbackTarget =>
            LowerLevelTarget != null
            && LowerLevelTarget != SameTierTarget
            && (SameTierTarget == null || HeightTransferActionSelector.IsStepHeightRejectReason(SameTierRejectReason));
    }
}
