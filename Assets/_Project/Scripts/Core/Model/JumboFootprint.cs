using System.Collections.Generic;
using UnityEngine;

namespace DiceGame.Core
{
    public static class JumboFootprint
    {
        public const int Size = 2;
        public const int CellCount = Size * Size;
        /// <summary>Match weight before sink erasure (one logical die).</summary>
        public const int MatchWeightBeforeErasure = 1;
        /// <summary>Match weight per tier while sink-erasing (Bottom 4 / Top 4).</summary>
        public const int MatchWeightPerTierWhileErasing = 4;

        public static void AppendCells(Vector2Int anchor, List<Vector2Int> results) {
            if (results == null) {
                return;
            }

            for (var dx = 0; dx < Size; dx++) {
                for (var dy = 0; dy < Size; dy++) {
                    results.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
                }
            }
        }

        public static bool Contains(Vector2Int anchor, Vector2Int cell) {
            return cell.x >= anchor.x
                && cell.x < anchor.x + Size
                && cell.y >= anchor.y
                && cell.y < anchor.y + Size;
        }

        public static Vector3 GetCenterWorldOffset(float cellSize) {
            var half = cellSize * 0.5f;
            return new Vector3(half, 0f, half);
        }

        public static float GetTopSurfaceHeightAboveFloor(float cellSize) {
            return cellSize * Size;
        }
    }
}
