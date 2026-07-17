using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    /// <summary>
    /// Builds <see cref="EffectiveDiceBehavior"/> from a live die + board adjacency.
    /// </summary>
    public static class DiceEffectiveBehaviorFactory
    {
        public static EffectiveDiceBehavior For(DiceController dice, DiceRegistry registry) {
            if (dice == null) {
                return default;
            }

            var baseBehavior = dice.Behavior;
            if (registry == null) {
                return DiceEffectiveBehaviorResolver.Resolve(
                    baseBehavior,
                    DiceBehaviorRuntimeState.WithoutBoard(
                        dice.IsRadianceErasing,
                        dice.IsSpawning,
                        dice.IsSinkErasing));
            }

            var state = new DiceBehaviorRuntimeState(
                dice.IsRadianceErasing,
                dice.IsSpawning,
                dice.IsSinkErasing,
                IronAdjacencyBlock.HasAdjacentMagnetBlocker(dice, registry));
            return DiceEffectiveBehaviorResolver.Resolve(baseBehavior, state);
        }
    }
}
