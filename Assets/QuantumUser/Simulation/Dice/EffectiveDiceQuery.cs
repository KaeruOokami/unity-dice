namespace Quantum
{
    using DiceGame.Core;
    using DiceGame.SimShared.Magnet;

    /// <summary>
    /// Frame adapter over production <see cref="DiceEffectiveBehaviorResolver"/>.
    /// </summary>
    public static unsafe class EffectiveDiceQuery
    {
        public static EffectiveDiceBehavior Resolve(Frame frame, in Dice dice)
        {
            // Prefer ResolveAt when cell is known (magnet adjacency is position-dependent).
            return ResolveCore(dice, hasAdjacentMagnetBlocker: false);
        }

        public static EffectiveDiceBehavior ResolveAt(
            Frame frame,
            EntityRef entity,
            in Dice dice,
            int cellX,
            int cellY)
        {
            var magnetBlock = HasAdjacentMagnetBlocker(frame, cellX, cellY, dice.Tier, entity);
            return ResolveCore(dice, magnetBlock);
        }

        static EffectiveDiceBehavior ResolveCore(in Dice dice, bool hasAdjacentMagnetBlocker)
        {
            var isSink = dice.IsErasing && dice.Tier == DiceStackTier.Bottom;
            var isRadiance = dice.IsErasing && dice.Tier == DiceStackTier.Top;
            return CoreDiceBridge.ResolveEffective(
                dice.Kind,
                dice.IsSpawning,
                isSink,
                isRadiance,
                hasAdjacentMagnetBlocker);
        }

        public static bool HasAdjacentMagnetBlocker(
            Frame frame,
            int cellX,
            int cellY,
            DiceStackTier tier,
            EntityRef ignore)
        {
            var tierNorm = tier == DiceStackTier.Top ? 1 : 0;
            return MagnetAdjacencyBlock.HasAdjacentMagnetBlocker(
                cellX,
                cellY,
                tierNorm,
                Query);

            bool Query(int x, int y, int t, out bool blocks, out bool erasing)
            {
                return TryGetSameTierNeighbor(frame, x, y, t, ignore, out blocks, out erasing);
            }
        }

        static bool TryGetSameTierNeighbor(
            Frame frame,
            int x,
            int y,
            int tierNorm,
            EntityRef ignore,
            out bool blocksAdjacentMagnet,
            out bool isErasing)
        {
            blocksAdjacentMagnet = false;
            isErasing = false;
            var tier = tierNorm == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            if (!CellOccupancy.TryGetAt(frame, x, y, tier, out var entity, out var dice)
                || entity == ignore
                || dice.IsCarried)
            {
                return false;
            }

            blocksAdjacentMagnet = CoreDiceBridge.GetCapabilities(dice.Kind).BlocksAdjacentMagnet;
            isErasing = dice.IsErasing;
            return true;
        }
    }
}
