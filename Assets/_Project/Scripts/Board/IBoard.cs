using UnityEngine;

namespace DiceGame.Grid
{
    public interface IBoard
    {
        int Width { get; }
        int Height { get; }
        float CellSize { get; }
        bool IsInside(Vector2Int gridPos);
        CellType GetCell(Vector2Int gridPos);
        float FloorSurfaceWorldY { get; }
        Vector3 GridToWorld(Vector2Int gridPos);
        Vector2Int WorldToGrid(Vector3 worldPosition);
    }
}
