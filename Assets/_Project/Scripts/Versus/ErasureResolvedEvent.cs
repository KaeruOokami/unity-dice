using DiceGame.Config;

namespace DiceGame.Versus
{
    public readonly struct ErasureResolvedEvent
    {
        public PlayerSlot Attacker { get; }
        public PlayerSlot Target { get; }
        public int Face { get; }
        public int ChainCount { get; }
        public int ClusterSize { get; }
        public bool IsSnatch { get; }

        public ErasureResolvedEvent(
            PlayerSlot attacker,
            PlayerSlot target,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch) {
            Attacker = attacker;
            Target = target;
            Face = face;
            ChainCount = chainCount;
            ClusterSize = clusterSize;
            IsSnatch = isSnatch;
        }
    }
}
