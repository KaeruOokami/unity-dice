using System.Collections.Generic;
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
        static readonly List<Vector2Int> FootprintBuffer = new(JumboFootprint.CellCount);

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

        /// <summary>
        /// Match adjacency against an occupant that may occupy a jumbo footprint.
        /// Same-tier orthogonal to any occupied cell counts.
        /// </summary>
        public static bool IsAdjacentForMatchToOccupant(
            DiceSlot work,
            Vector2Int occupantAnchor,
            DiceStackTier occupantTier,
            bool hasExpandedFootprint) {
            if (work.Tier != occupantTier) {
                return false;
            }

            if (!hasExpandedFootprint) {
                return IsAdjacentForMatch(work, new DiceSlot(occupantAnchor, occupantTier));
            }

            FootprintBuffer.Clear();
            JumboFootprint.AppendCells(occupantAnchor, FootprintBuffer);
            for (var i = 0; i < FootprintBuffer.Count; i++) {
                if (IsAdjacentForMatch(work, new DiceSlot(FootprintBuffer[i], occupantTier))) {
                    return true;
                }
            }

            return false;
        }

        public static int ManhattanToOccupant(
            Vector2Int from,
            Vector2Int occupantAnchor,
            bool hasExpandedFootprint) {
            if (!hasExpandedFootprint) {
                return Mathf.Abs(from.x - occupantAnchor.x) + Mathf.Abs(from.y - occupantAnchor.y);
            }

            FootprintBuffer.Clear();
            JumboFootprint.AppendCells(occupantAnchor, FootprintBuffer);
            var best = int.MaxValue;
            for (var i = 0; i < FootprintBuffer.Count; i++) {
                var cell = FootprintBuffer[i];
                var distance = Mathf.Abs(from.x - cell.x) + Mathf.Abs(from.y - cell.y);
                if (distance < best) {
                    best = distance;
                }
            }

            return best;
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
