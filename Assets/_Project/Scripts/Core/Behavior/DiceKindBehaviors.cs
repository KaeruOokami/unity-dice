namespace DiceGame.Core
{
    /// <summary>
    /// Per-kind behavior singletons. Open the matching class to see that kind's rules.
    /// </summary>
    public sealed class NormalDiceBehavior : DiceBehaviorBase
    {
        public static readonly NormalDiceBehavior Instance = new();

        NormalDiceBehavior() {
        }

        public override DiceKind Kind => DiceKind.Normal;

        public override DiceCapabilities Capabilities { get; } = new(
            canBePushedByPlayer: true,
            canBeLiftedByPlayer: true,
            canJumpCoupleWithPlayer: true,
            pushUsesRoll: false,
            canGridRoll: true,
            slideUntilBlocked: false,
            hasMagnetCoupling: false,
            hasSpawnBounce: true,
            rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier);
    }

    public sealed class WoodDiceBehavior : DiceBehaviorBase
    {
        public static readonly WoodDiceBehavior Instance = new();

        WoodDiceBehavior() {
        }

        public override DiceKind Kind => DiceKind.Wood;

        public override DiceCapabilities Capabilities { get; } = new(
            canBePushedByPlayer: true,
            canBeLiftedByPlayer: true,
            canJumpCoupleWithPlayer: true,
            pushUsesRoll: true,
            canGridRoll: true,
            slideUntilBlocked: false,
            hasMagnetCoupling: false,
            hasSpawnBounce: true,
            rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier);
    }

    public sealed class IronDiceBehavior : DiceBehaviorBase
    {
        public static readonly IronDiceBehavior Instance = new();

        IronDiceBehavior() {
        }

        public override DiceKind Kind => DiceKind.Iron;

        public override DiceCapabilities Capabilities { get; } = new(
            canBePushedByPlayer: false,
            canBeLiftedByPlayer: false,
            canJumpCoupleWithPlayer: false,
            pushUsesRoll: false,
            canGridRoll: false,
            slideUntilBlocked: false,
            hasMagnetCoupling: false,
            hasSpawnBounce: false,
            rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier,
            blocksAdjacentMagnet: true);
    }

    public sealed class MagnetDiceBehavior : DiceBehaviorBase
    {
        public static readonly MagnetDiceBehavior Instance = new();

        MagnetDiceBehavior() {
        }

        public override DiceKind Kind => DiceKind.Magnet;

        public override DiceCapabilities Capabilities { get; } = new(
            canBePushedByPlayer: true,
            canBeLiftedByPlayer: true,
            canJumpCoupleWithPlayer: true,
            pushUsesRoll: false,
            canGridRoll: true,
            slideUntilBlocked: false,
            hasMagnetCoupling: true,
            hasSpawnBounce: true,
            rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier);
    }

    public sealed class IceDiceBehavior : DiceBehaviorBase
    {
        public static readonly IceDiceBehavior Instance = new();

        IceDiceBehavior() {
        }

        public override DiceKind Kind => DiceKind.Ice;

        public override DiceCapabilities Capabilities { get; } = new(
            canBePushedByPlayer: true,
            canBeLiftedByPlayer: true,
            canJumpCoupleWithPlayer: true,
            pushUsesRoll: false,
            canGridRoll: false,
            slideUntilBlocked: true,
            hasMagnetCoupling: false,
            hasSpawnBounce: true,
            rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier,
            blocksJumpTransferToOtherDice: true,
            blocksJumpUpwardTierChange: true,
            maxJumpGridMoveDistance: 1,
            preservesOrientationOnGridMove: true,
            usesSlideVisualForDemote: true);
    }

    public sealed class StoneDiceBehavior : DiceBehaviorBase
    {
        public static readonly StoneDiceBehavior Instance = new();

        StoneDiceBehavior() {
        }

        public override DiceKind Kind => DiceKind.Stone;

        public override DiceCapabilities Capabilities { get; } = new(
            canBePushedByPlayer: false,
            canBeLiftedByPlayer: false,
            canJumpCoupleWithPlayer: false,
            pushUsesRoll: false,
            canGridRoll: true,
            slideUntilBlocked: false,
            hasMagnetCoupling: false,
            hasSpawnBounce: false,
            rollDurationMultiplier: DiceBehaviorConstants.StoneRollDurationMultiplier);
    }

    public sealed class GhostDiceBehavior : DiceBehaviorBase
    {
        public static readonly GhostDiceBehavior Instance = new();

        GhostDiceBehavior() {
        }

        public override DiceKind Kind => DiceKind.Ghost;

        public override DiceCapabilities Capabilities { get; } = new(
            canBePushedByPlayer: false,
            canBeLiftedByPlayer: false,
            canJumpCoupleWithPlayer: false,
            pushUsesRoll: false,
            canGridRoll: false,
            slideUntilBlocked: false,
            hasMagnetCoupling: false,
            hasSpawnBounce: false,
            rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier,
            spawnGravityScale: DiceBehaviorConstants.GhostSpawnGravityScale,
            isPlayerPassThrough: true,
            allowsDiceSwapThrough: true);
    }
}
