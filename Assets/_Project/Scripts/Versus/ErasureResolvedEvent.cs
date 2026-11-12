using DiceGame.Config;
using UnityEngine;

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
        public Vector3 OriginWorldPosition { get; }

        public ErasureResolvedEvent(
            PlayerSlot attacker,
            PlayerSlot target,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch,
            Vector3 originWorldPosition) {
            Attacker = attacker;
            Target = target;
            Face = face;
            ChainCount = chainCount;
            ClusterSize = clusterSize;
            IsSnatch = isSnatch;
            OriginWorldPosition = originWorldPosition;
        }
    }
}
