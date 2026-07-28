using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Normalized inputs for <see cref="MoveActionSelector"/>.
    /// Probe results used only as boolean/plan facts — not as fall-through signals.
    /// </summary>
    public readonly struct MoveFacts
    {
        public Vector2Int FromCell { get; }
        public Vector2Int ToCell { get; }
        public int FromLevel { get; }
        public DiceController StandingDice { get; }
        public BoardSurface FromSurface { get; }
        public Direction Direction { get; }
        public PassabilityContext Context { get; }
        public HeightReachEvaluation Reach { get; }

        public bool IsJumping { get; }
        public DiceStandingMoveMode Mode { get; }

        public DiceController TargetDice { get; }
        public int TargetLevel { get; }
        public float TargetSurfaceWorldY { get; }
        public MoveLevelRelation Relation { get; }

        public bool WithinReachFull { get; }
        public bool WithinReachDescentOnly { get; }

        public bool HasExpandedFootprintWalk { get; }
        public MovementTransition ExpandedFootprintTransition { get; }

        public bool BlocksDiceCoupledStackEntry { get; }
        public bool IsPlayerFloorPassable { get; }
        public bool CanPlaceBottomAtToCell { get; }
        public bool ToCellIsOccupiedForCoupled => !CanPlaceBottomAtToCell;

        /// <summary>Floor→Bottom mount target (pending included). Independent of top-first support.</summary>
        public DiceController FloorMountBottomDice { get; }

        public bool HasIceSlideDisplacement { get; }
        public DiceSlidePlan IceSlidePlan { get; }
        public DiceController IceElasticTarget { get; }

        public bool CanJumpGridRoll { get; }
        public MovementTransition JumpGridTransition { get; }

        public bool CanTopFall { get; }
        public MovementTransition TopFallTransition { get; }

        public bool CanTierLand { get; }
        public MovementTransition TierLandingTransition { get; }

        public bool CanGridRoll { get; }
        public DiceGridMovePlan GridRollPlan { get; }

        public MoveFacts(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            DiceController standingDice,
            BoardSurface fromSurface,
            Direction direction,
            PassabilityContext context,
            HeightReachEvaluation reach,
            bool isJumping,
            DiceStandingMoveMode mode,
            DiceController targetDice,
            int targetLevel,
            float targetSurfaceWorldY,
            MoveLevelRelation relation,
            bool withinReachFull,
            bool withinReachDescentOnly,
            bool hasExpandedFootprintWalk,
            MovementTransition expandedFootprintTransition,
            bool blocksDiceCoupledStackEntry,
            bool isPlayerFloorPassable,
            bool canPlaceBottomAtToCell,
            DiceController floorMountBottomDice,
            bool hasIceSlideDisplacement,
            DiceSlidePlan iceSlidePlan,
            DiceController iceElasticTarget,
            bool canJumpGridRoll,
            MovementTransition jumpGridTransition,
            bool canTopFall,
            MovementTransition topFallTransition,
            bool canTierLand,
            MovementTransition tierLandingTransition,
            bool canGridRoll,
            DiceGridMovePlan gridRollPlan) {
            FromCell = fromCell;
            ToCell = toCell;
            FromLevel = fromLevel;
            StandingDice = standingDice;
            FromSurface = fromSurface;
            Direction = direction;
            Context = context;
            Reach = reach;
            IsJumping = isJumping;
            Mode = mode;
            TargetDice = targetDice;
            TargetLevel = targetLevel;
            TargetSurfaceWorldY = targetSurfaceWorldY;
            Relation = relation;
            WithinReachFull = withinReachFull;
            WithinReachDescentOnly = withinReachDescentOnly;
            HasExpandedFootprintWalk = hasExpandedFootprintWalk;
            ExpandedFootprintTransition = expandedFootprintTransition;
            BlocksDiceCoupledStackEntry = blocksDiceCoupledStackEntry;
            IsPlayerFloorPassable = isPlayerFloorPassable;
            CanPlaceBottomAtToCell = canPlaceBottomAtToCell;
            FloorMountBottomDice = floorMountBottomDice;
            HasIceSlideDisplacement = hasIceSlideDisplacement;
            IceSlidePlan = iceSlidePlan;
            IceElasticTarget = iceElasticTarget;
            CanJumpGridRoll = canJumpGridRoll;
            JumpGridTransition = jumpGridTransition;
            CanTopFall = canTopFall;
            TopFallTransition = topFallTransition;
            CanTierLand = canTierLand;
            TierLandingTransition = tierLandingTransition;
            CanGridRoll = canGridRoll;
            GridRollPlan = gridRollPlan;
        }
    }
}
