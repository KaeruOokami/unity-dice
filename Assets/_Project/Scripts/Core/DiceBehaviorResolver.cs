namespace DiceGame.Core
{
    public static class DiceBehaviorResolver
    {
        public static DiceCapabilities GetCapabilities(DiceKind kind) {
            return kind switch {
                DiceKind.Wood => new DiceCapabilities(
                    canBePushedByPlayer: true,
                    canBeLiftedByPlayer: true,
                    canJumpCoupleWithPlayer: true,
                    pushUsesRoll: true,
                    canGridRoll: true,
                    slideUntilBlocked: false,
                    hasMagnetCoupling: false,
                    hasSpawnBounce: true,
                    rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier),
                DiceKind.Iron => new DiceCapabilities(
                    canBePushedByPlayer: false,
                    canBeLiftedByPlayer: false,
                    canJumpCoupleWithPlayer: false,
                    pushUsesRoll: false,
                    canGridRoll: false,
                    slideUntilBlocked: false,
                    hasMagnetCoupling: false,
                    hasSpawnBounce: false,
                    rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier),
                DiceKind.Magnet => new DiceCapabilities(
                    canBePushedByPlayer: true,
                    canBeLiftedByPlayer: true,
                    canJumpCoupleWithPlayer: true,
                    pushUsesRoll: false,
                    canGridRoll: true,
                    slideUntilBlocked: false,
                    hasMagnetCoupling: true,
                    hasSpawnBounce: true,
                    rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier),
                DiceKind.Ice => new DiceCapabilities(
                    canBePushedByPlayer: true,
                    canBeLiftedByPlayer: true,
                    canJumpCoupleWithPlayer: true,
                    pushUsesRoll: false,
                    canGridRoll: false,
                    slideUntilBlocked: true,
                    hasMagnetCoupling: false,
                    hasSpawnBounce: true,
                    rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier),
                DiceKind.Stone => new DiceCapabilities(
                    canBePushedByPlayer: false,
                    canBeLiftedByPlayer: false,
                    canJumpCoupleWithPlayer: false,
                    pushUsesRoll: false,
                    canGridRoll: true,
                    slideUntilBlocked: false,
                    hasMagnetCoupling: false,
                    hasSpawnBounce: false,
                    rollDurationMultiplier: DiceBehaviorConstants.StoneRollDurationMultiplier),
                _ => new DiceCapabilities(
                    canBePushedByPlayer: true,
                    canBeLiftedByPlayer: true,
                    canJumpCoupleWithPlayer: true,
                    pushUsesRoll: false,
                    canGridRoll: true,
                    slideUntilBlocked: false,
                    hasMagnetCoupling: false,
                    hasSpawnBounce: true,
                    rollDurationMultiplier: DiceBehaviorConstants.DefaultRollDurationMultiplier)
            };
        }
    }
}
