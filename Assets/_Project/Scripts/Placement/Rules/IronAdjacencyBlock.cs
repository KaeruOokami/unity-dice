using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.SimShared.Magnet;

namespace DiceGame.Placement
{
    /// <summary>
    /// Board query for magnet-blocking adjacency. Movable/couple resolution lives in
    /// <see cref="DiceEffectiveBehaviorResolver"/>.
    /// </summary>
    public static class IronAdjacencyBlock
    {
        public static bool IsPlayerMovable(DiceController dice, DiceRegistry registry)
        {
            return DiceEffectiveBehaviorFactory.For(dice, registry).IsPlayerMovable;
        }

        public static bool CanJumpCoupleWithPlayer(DiceController dice, DiceRegistry registry)
        {
            if (dice == null)
            {
                return true;
            }

            return DiceEffectiveBehaviorFactory.For(dice, registry).CanJumpCoupleWithPlayer;
        }

        public static bool HasAdjacentMagnetBlocker(DiceController dice, DiceRegistry registry)
        {
            if (dice == null || registry == null)
            {
                return false;
            }

            var tier = dice.CurrentState.Tier;
            var cell = dice.CurrentState.GridPos;
            var tierNorm = tier == DiceStackTier.Top ? 1 : 0;

            return MagnetAdjacencyBlock.HasAdjacentMagnetBlocker(
                cell.x,
                cell.y,
                tierNorm,
                Query);

            bool Query(int x, int y, int t, out bool blocks, out bool erasing)
            {
                blocks = false;
                erasing = false;
                var stackTier = t == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
                if (!registry.TryGetDiceAt(new UnityEngine.Vector2Int(x, y), stackTier, out var neighbor)
                    || neighbor == null)
                {
                    return false;
                }

                blocks = neighbor.Capabilities.BlocksAdjacentMagnet;
                erasing = neighbor.IsErasing || neighbor.IsVanishing;
                return true;
            }
        }
    }
}
