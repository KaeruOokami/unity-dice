using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public enum SurfaceLayer
    {
        Floor,
        Bottom,
        Top
    }

    public enum MovementTransitionKind
    {
        Walkable,
        Blocked,
        CanRoll,
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
        DissolveDescent
    }

    public readonly struct MovementTransition
    {
        public MovementTransitionKind Kind { get; }
        public MovementTransitionRoute Route { get; }
        public DiceController TargetDice { get; }
        public SurfaceLayer TargetLayer { get; }
        public bool HasDiceGridMovePlan { get; }
        public DiceGridMovePlan DiceGridMovePlan { get; }

        MovementTransition(
            MovementTransitionKind kind,
            MovementTransitionRoute route,
            DiceController targetDice,
            SurfaceLayer targetLayer,
            bool hasDiceGridMovePlan,
            DiceGridMovePlan diceGridMovePlan) {
            Kind = kind;
            Route = route;
            TargetDice = targetDice;
            TargetLayer = targetLayer;
            HasDiceGridMovePlan = hasDiceGridMovePlan;
            DiceGridMovePlan = diceGridMovePlan;
        }

        public static MovementTransition Walkable(
            DiceController dice,
            SurfaceLayer layer,
            MovementTransitionRoute route = MovementTransitionRoute.HeightTransfer) {
            return new MovementTransition(
                MovementTransitionKind.Walkable,
                route,
                dice,
                layer,
                false,
                default);
        }

        public static MovementTransition WalkableWithGridPlan(
            DiceController dice,
            SurfaceLayer layer,
            MovementTransitionRoute route,
            DiceGridMovePlan plan) {
            return new MovementTransition(
                MovementTransitionKind.Walkable,
                route,
                dice,
                layer,
                true,
                plan);
        }

        public static MovementTransition Blocked() {
            return new MovementTransition(
                MovementTransitionKind.Blocked,
                MovementTransitionRoute.None,
                null,
                SurfaceLayer.Floor,
                false,
                default);
        }

        public static MovementTransition GridRoll(DiceGridMovePlan plan) {
            return new MovementTransition(
                MovementTransitionKind.CanRoll,
                MovementTransitionRoute.DiceRoll,
                null,
                SurfaceLayer.Floor,
                true,
                plan);
        }

        public static MovementTransition BlockedStepOnly(DiceController targetDice, SurfaceLayer targetLayer) {
            return new MovementTransition(
                MovementTransitionKind.BlockedStepOnly,
                MovementTransitionRoute.DissolveDescent,
                targetDice,
                targetLayer,
                false,
                default);
        }

        public bool IsDissolveDescentHold =>
            Kind == MovementTransitionKind.BlockedStepOnly
            && Route == MovementTransitionRoute.DissolveDescent;

        public bool IsDissolveDescentToFloor =>
            IsDissolveDescentHold && TargetLayer == SurfaceLayer.Floor;
    }
}
