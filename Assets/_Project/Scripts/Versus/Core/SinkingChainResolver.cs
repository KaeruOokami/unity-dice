using DiceGame.Config;

namespace DiceGame.Versus.Core
{
    public readonly struct SinkingChainResult
    {
        public int ChainCount { get; }
        public bool IsSnatch { get; }

        public SinkingChainResult(int chainCount, bool isSnatch) {
            ChainCount = chainCount;
            IsSnatch = isSnatch;
        }
    }

    public static class SinkingChainResolver
    {
        public static SinkingChainResult Resolve(
            int maxExistingChainCount,
            PlayerSlot lastAttacker,
            PlayerSlot currentAttacker,
            bool hasExistingSinkingDice,
            bool mergesMultipleGroups) {
            if (!hasExistingSinkingDice) {
                return new SinkingChainResult(0, false);
            }

            var chainCount = mergesMultipleGroups
                ? maxExistingChainCount
                : maxExistingChainCount + 1;
            var isSnatch = lastAttacker != currentAttacker;
            return new SinkingChainResult(chainCount, isSnatch);
        }

        public static PlayerSlot GetOpponent(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? PlayerSlot.Player2 : PlayerSlot.Player1;
        }
    }
}
