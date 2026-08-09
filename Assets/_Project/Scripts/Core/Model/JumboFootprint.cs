using System.Collections.Generic;
using UnityEngine;

namespace DiceGame.Core
{
    /// <summary>
    /// Unity-facing jumbo helpers; cell math lives in <see cref="JumboFootprintCells"/>.
    /// </summary>
    public static class JumboFootprint
    {
        public const int Size = JumboFootprintCells.Size;
        public const int CellCount = JumboFootprintCells.CellCount;
        public const int MatchWeightBeforeErasure = JumboFootprintCells.MatchWeightBeforeErasure;
        public const int MatchWeightPerTierWhileErasing = JumboFootprintCells.MatchWeightPerTierWhileErasing;
        public const float SinkTopOccupancyThreshold = JumboFootprintCells.SinkTopOccupancyThreshold;

        public static void AppendCells(Vector2Int anchor, List<Vector2Int> results)
        {
            if (results == null)
            {
                return;
            }

            var xs = new int[CellCount];
            var ys = new int[CellCount];
            JumboFootprintCells.AppendCells(anchor.x, anchor.y, xs, ys, out var count);
            for (var i = 0; i < count; i++)
            {
                results.Add(new Vector2Int(xs[i], ys[i]));
            }
        }

        public static bool Contains(Vector2Int anchor, Vector2Int cell)
        {
            return JumboFootprintCells.Contains(anchor.x, anchor.y, cell.x, cell.y);
        }

        public static Vector3 GetCenterWorldOffset(float cellSize)
        {
            var half = cellSize * 0.5f;
            return new Vector3(half, 0f, half);
        }

        public static float GetTopSurfaceHeightAboveFloor(float cellSize)
        {
            return cellSize * Size;
        }
    }
}
