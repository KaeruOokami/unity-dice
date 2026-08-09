namespace Quantum
{
    using CoreCaps = DiceGame.Core.DiceCapabilities;
    using CoreKind = DiceGame.Core.DiceKind;
    using DiceGame.Core;

    /// <summary>
    /// Maps Quantum DSL kinds to production <see cref="DiceBehaviorResolver"/> (no lite Capabilities copy).
    /// </summary>
    public static class CoreDiceBridge
    {
        public static CoreKind ToCoreKind(DiceKind kind)
        {
            return (CoreKind)(int)kind;
        }

        public static DiceGame.Core.DiceStackTier ToCoreTier(DiceStackTier tier)
        {
            return (DiceGame.Core.DiceStackTier)(int)tier;
        }

        public static DiceStackTier ToQuantumTier(DiceGame.Core.DiceStackTier tier)
        {
            return (DiceStackTier)(int)tier;
        }

        public static DiceGame.Core.DiceOrientation ToCoreOrientation(int topFace, int northFace, int eastFace)
        {
            return new DiceGame.Core.DiceOrientation(topFace, northFace, eastFace);
        }

        public static CoreCaps GetCapabilities(DiceKind kind)
        {
            return DiceBehaviorResolver.GetCapabilities(ToCoreKind(kind));
        }

        public static IDiceBehavior GetBehavior(DiceKind kind)
        {
            return DiceBehaviorResolver.GetBehavior(ToCoreKind(kind));
        }

        public static EffectiveDiceBehavior ResolveEffective(
            DiceKind kind,
            bool isSpawning,
            bool isSinkErasing,
            bool isRadianceErasing,
            bool hasAdjacentMagnetBlocker)
        {
            var behavior = GetBehavior(kind);
            var state = new DiceBehaviorRuntimeState(
                isRadianceErasing,
                isSpawning,
                isSinkErasing,
                hasAdjacentMagnetBlocker);
            return DiceEffectiveBehaviorResolver.Resolve(behavior, in state);
        }
    }
}
