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
        CanRoll
    }

    public readonly struct MovementTransition
    {
        public MovementTransitionKind Kind { get; }
        public DiceController TargetDice { get; }
        public SurfaceLayer TargetLayer { get; }

        MovementTransition(
            MovementTransitionKind kind,
            DiceController targetDice,
            SurfaceLayer targetLayer) {
            Kind = kind;
            TargetDice = targetDice;
            TargetLayer = targetLayer;
        }

        public static MovementTransition Walkable(DiceController dice, SurfaceLayer layer) {
            return new MovementTransition(MovementTransitionKind.Walkable, dice, layer);
        }

        public static MovementTransition Blocked() {
            return new MovementTransition(MovementTransitionKind.Blocked, null, SurfaceLayer.Floor);
        }

        public static MovementTransition Roll() {
            return new MovementTransition(MovementTransitionKind.CanRoll, null, SurfaceLayer.Floor);
        }
    }
}
