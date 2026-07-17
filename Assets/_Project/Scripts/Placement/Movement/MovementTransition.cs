using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public enum MovementTransitionKind
    {
        Walkable,
        Blocked,
        CanRoll,
        IceSlide,
        BlockedStepOnly
    }

    public enum MovementTransitionRoute
    {
        None,
        HeightTransfer,
        TierLanding,
        DiceRoll,
        CoupledGridMove,
        FloorTransfer,
        TopFall,
        DissolveDescent,
        IceSlide
    }

    public readonly struct MovementTransition
    {
        public MovementTransitionKind Kind { get; }
        public MovementTransitionRoute Route { get; }
        public DiceController TargetDice { get; }
        public int TargetLevel { get; }
        public bool HasDiceGridMovePlan { get; }
        public DiceGridMovePlan DiceGridMovePlan { get; }
        public bool HasDiceSlidePlan { get; }
        public DiceSlidePlan DiceSlidePlan { get; }

        MovementTransition(
            MovementTransitionKind kind,
            MovementTransitionRoute route,
            DiceController targetDice,
            int targetLevel,
            bool hasDiceGridMovePlan,
            DiceGridMovePlan diceGridMovePlan,
            bool hasDiceSlidePlan,
            DiceSlidePlan diceSlidePlan) {
            Kind = kind;
            Route = route;
            TargetDice = targetDice;
            TargetLevel = targetLevel;
            HasDiceGridMovePlan = hasDiceGridMovePlan;
            DiceGridMovePlan = diceGridMovePlan;
            HasDiceSlidePlan = hasDiceSlidePlan;
            DiceSlidePlan = diceSlidePlan;
        }

        public static MovementTransition Walkable(
            DiceController dice,
            int targetLevel,
            MovementTransitionRoute route = MovementTransitionRoute.HeightTransfer) {
            return new MovementTransition(
                MovementTransitionKind.Walkable,
                route,
                dice,
                targetLevel,
                false,
                default,
                false,
                default);
        }

        public static MovementTransition WalkableWithGridPlan(
            DiceController dice,
            int targetLevel,
            MovementTransitionRoute route,
            DiceGridMovePlan plan) {
            return new MovementTransition(
                MovementTransitionKind.Walkable,
                route,
                dice,
                targetLevel,
                true,
                plan,
                false,
                default);
        }

        public static MovementTransition Blocked() {
            return new MovementTransition(
                MovementTransitionKind.Blocked,
                MovementTransitionRoute.None,
                null,
                SurfaceHeightLevel.Floor,
                false,
                default,
                false,
                default);
        }

        public static MovementTransition GridRoll(DiceGridMovePlan plan) {
            return new MovementTransition(
                MovementTransitionKind.CanRoll,
                MovementTransitionRoute.DiceRoll,
                null,
                SurfaceHeightLevel.Floor,
                true,
                plan,
                false,
                default);
        }

        public static MovementTransition IceSlide(
            DiceSlidePlan plan,
            DiceController elasticTransferTarget = null) {
            return new MovementTransition(
                MovementTransitionKind.IceSlide,
                MovementTransitionRoute.IceSlide,
                elasticTransferTarget,
                SurfaceHeightLevel.Floor,
                false,
                default,
                true,
                plan);
        }

        public static MovementTransition BlockedStepOnly(DiceController targetDice, int targetLevel) {
            return new MovementTransition(
                MovementTransitionKind.BlockedStepOnly,
                MovementTransitionRoute.DissolveDescent,
                targetDice,
                targetLevel,
                false,
                default,
                false,
                default);
        }

        public bool IsDissolveDescentHold =>
            Kind == MovementTransitionKind.BlockedStepOnly
            && Route == MovementTransitionRoute.DissolveDescent;

        public bool IsDissolveDescentToFloor =>
            IsDissolveDescentHold && TargetLevel == SurfaceHeightLevel.Floor;
    }
}
