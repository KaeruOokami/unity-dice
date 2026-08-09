namespace DiceGame.SimShared.Move
{
    using DiceGame.Core;
    /// <summary>
    /// Copied from production <c>MoveFacts</c> as Domain bool/int facts (no Unity / DiceController).
    /// </summary>
    public readonly struct MoveFacts
    {
        public int FromLevel { get; }
        public int TargetLevel { get; }
        public bool IsJumping { get; }
        public DiceStandingMoveMode Mode { get; }
        public bool HasStandingDice { get; }
        public bool CanJumpCoupleWithPlayer { get; }
        public bool IsSinkErasing { get; }
        public bool IsPlayerMovable { get; }
        public bool WithinReachFull { get; }
        public bool WithinReachDescentOnly { get; }
        public bool HasExpandedFootprintWalk { get; }
        public bool BlocksDiceCoupledStackEntry { get; }
        public bool IsPlayerFloorPassable { get; }
        public bool CanPlaceBottomAtToCell { get; }
        public bool HasFloorMountBottom { get; }
        public bool HasIceSlideDisplacement { get; }
        public bool CanJumpGridRoll { get; }
        public bool CanTopFall { get; }
        public bool CanTierLand { get; }
        public bool CanGridRoll { get; }
        public bool AllowJumpGridMove { get; }

        public bool ToCellIsOccupiedForCoupled => !CanPlaceBottomAtToCell;

        public MoveLevelRelation Relation => MoveLevelRelationRules.Resolve(FromLevel, TargetLevel);

        public MoveFacts(
            int fromLevel,
            int targetLevel,
            bool isJumping,
            DiceStandingMoveMode mode,
            bool hasStandingDice,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing,
            bool isPlayerMovable,
            bool withinReachFull,
            bool withinReachDescentOnly,
            bool hasExpandedFootprintWalk,
            bool blocksDiceCoupledStackEntry,
            bool isPlayerFloorPassable,
            bool canPlaceBottomAtToCell,
            bool hasFloorMountBottom,
            bool hasIceSlideDisplacement,
            bool canJumpGridRoll,
            bool canTopFall,
            bool canTierLand,
            bool canGridRoll,
            bool allowJumpGridMove)
        {
            FromLevel = fromLevel;
            TargetLevel = targetLevel;
            IsJumping = isJumping;
            Mode = mode;
            HasStandingDice = hasStandingDice;
            CanJumpCoupleWithPlayer = canJumpCoupleWithPlayer;
            IsSinkErasing = isSinkErasing;
            IsPlayerMovable = isPlayerMovable;
            WithinReachFull = withinReachFull;
            WithinReachDescentOnly = withinReachDescentOnly;
            HasExpandedFootprintWalk = hasExpandedFootprintWalk;
            BlocksDiceCoupledStackEntry = blocksDiceCoupledStackEntry;
            IsPlayerFloorPassable = isPlayerFloorPassable;
            CanPlaceBottomAtToCell = canPlaceBottomAtToCell;
            HasFloorMountBottom = hasFloorMountBottom;
            HasIceSlideDisplacement = hasIceSlideDisplacement;
            CanJumpGridRoll = canJumpGridRoll;
            CanTopFall = canTopFall;
            CanTierLand = canTierLand;
            CanGridRoll = canGridRoll;
            AllowJumpGridMove = allowJumpGridMove;
        }
    }

    public enum MoveLevelRelation
    {
        Same = 0,
        Ascent = 1,
        Descent = 2,
        BottomToTop = 3,
    }

    public static class MoveLevelRelationRules
    {
        public static MoveLevelRelation Resolve(int fromLevel, int targetLevel)
        {
            if (fromLevel == targetLevel)
            {
                return MoveLevelRelation.Same;
            }

            if (fromLevel == Placement.SurfaceHeightNorms.Bottom
                && targetLevel == Placement.SurfaceHeightNorms.Top)
            {
                return MoveLevelRelation.BottomToTop;
            }

            return targetLevel < fromLevel ? MoveLevelRelation.Descent : MoveLevelRelation.Ascent;
        }
    }
}
