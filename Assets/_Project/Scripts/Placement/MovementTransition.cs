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

        MovementTransition(
            MovementTransitionKind kind,
            MovementTransitionRoute route,
            DiceController targetDice,
            SurfaceLayer targetLayer) {
            Kind = kind;
            Route = route;
            TargetDice = targetDice;
            TargetLayer = targetLayer;
        }

        public static MovementTransition Walkable(
            DiceController dice,
            SurfaceLayer layer,
            MovementTransitionRoute route = MovementTransitionRoute.HeightTransfer) {
            return new MovementTransition(MovementTransitionKind.Walkable, route, dice, layer);
        }

        public static MovementTransition Blocked() {
            return new MovementTransition(
                MovementTransitionKind.Blocked,
                MovementTransitionRoute.None,
                null,
                SurfaceLayer.Floor);
        }

        public static MovementTransition Roll() {
            return new MovementTransition(
                MovementTransitionKind.CanRoll,
                MovementTransitionRoute.DiceRoll,
                null,
                SurfaceLayer.Floor);
        }

        public static MovementTransition BlockedStepOnly(DiceController targetDice, SurfaceLayer targetLayer) {
            return new MovementTransition(
                MovementTransitionKind.BlockedStepOnly,
                MovementTransitionRoute.DissolveDescent,
                targetDice,
                targetLayer);
        }

        public bool IsDissolveDescentToFloor =>
            Kind == MovementTransitionKind.BlockedStepOnly && TargetLayer == SurfaceLayer.Floor;
    }
}
