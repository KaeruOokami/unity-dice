namespace DiceGame.Core
{
    /// <summary>
    /// Runtime overlays applied on top of kind-static <see cref="IDiceBehavior"/>.
    /// </summary>
    public readonly struct DiceBehaviorRuntimeState
    {
        public bool IsRadianceErasing { get; }
        public bool IsSpawning { get; }
        public bool IsSinkErasing { get; }
        /// <summary>True when an adjacent die has <see cref="DiceCapabilities.BlocksAdjacentMagnet"/>.</summary>
        public bool HasAdjacentMagnetBlocker { get; }

        public DiceBehaviorRuntimeState(
            bool isRadianceErasing,
            bool isSpawning,
            bool isSinkErasing,
            bool hasAdjacentMagnetBlocker) {
            IsRadianceErasing = isRadianceErasing;
            IsSpawning = isSpawning;
            IsSinkErasing = isSinkErasing;
            HasAdjacentMagnetBlocker = hasAdjacentMagnetBlocker;
        }

        public static DiceBehaviorRuntimeState WithoutBoard(
            bool isRadianceErasing,
            bool isSpawning,
            bool isSinkErasing) {
            return new DiceBehaviorRuntimeState(
                isRadianceErasing,
                isSpawning,
                isSinkErasing,
                hasAdjacentMagnetBlocker: false);
        }
    }
}
