using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public readonly struct DiceSlot {
        public Vector2Int Cell { get; }
        public DiceStackTier Tier { get; }

        public DiceSlot(Vector2Int cell, DiceStackTier tier) {
            Cell = cell;
            Tier = tier;
        }

        public static DiceSlot FromDice(DiceController dice) {
            var state = dice.CurrentState;
            return new DiceSlot(state.GridPos, state.Tier);
        }
    }

    public static class DiceStackAdjacency {
        public static (int dx, int dy, int dz) GetDelta(DiceSlot from, DiceSlot to) {
            return (
                Mathf.Abs(to.Cell.x - from.Cell.x),
                Mathf.Abs(to.Cell.y - from.Cell.y),
                Mathf.Abs((int)to.Tier - (int)from.Tier));
        }

        public static bool Is3DOrthogonallyAdjacent(DiceSlot from, DiceSlot to) {
            var (dx, dy, dz) = GetDelta(from, to);
            return dx + dy + dz == 1;
        }

        public static bool IsHorizontalGridAdjacent(DiceSlot from, DiceSlot to) {
            var (dx, dy, dz) = GetDelta(from, to);
            return dz == 0 && dx + dy == 1;
        }

        public static bool IsAdjacentForMatch(DiceSlot from, DiceSlot to) {
            var (dx, dy, dz) = GetDelta(from, to);
            return from.Tier == to.Tier && dz == 0 && dx + dy == 1;
        }

        public static bool IsAdjacentForLift(DiceSlot player, DiceSlot dice) {
            var (dx, dy, _) = GetDelta(player, dice);
            return dx + dy == 1;
        }

        public static bool IsAdjacentForPush(DiceSlot player, DiceSlot dice, bool isOnFloor) {
            if (isOnFloor) {
                return dice.Tier == DiceStackTier.Bottom
                    && IsHorizontalGridAdjacent(player, dice);
            }

            if (player.Tier == DiceStackTier.Bottom) {
                return dice.Tier == DiceStackTier.Top
                    && IsHorizontallyAdjacentTopLayer(player, dice);
            }

            return dice.Tier == DiceStackTier.Top
                && IsHorizontalGridAdjacent(player, dice);
        }

        static bool IsHorizontallyAdjacentTopLayer(DiceSlot player, DiceSlot dice) {
            var (dx, dy, _) = GetDelta(player, dice);
            return dx + dy == 1;
        }
    }
}
