using UnityEngine;

namespace DiceGame.Grid
{
    public interface IBoard
    {
        int Width { get; }
        int Height { get; }
        float CellSize { get; }
        bool IsInside(Vector2Int gridPos);
        bool HasDiceAt(Vector2Int gridPos);
        bool CanDiceRollInto(Vector2Int gridPos);
        float FloorSurfaceWorldY { get; }
        Vector3 GridToWorld(Vector2Int gridPos);
        Vector2Int WorldToGrid(Vector3 worldPosition);
        void RegisterDice(Vector2Int gridPos);
        void UnregisterDice(Vector2Int gridPos);
        void MoveDice(Vector2Int from, Vector2Int to);
    }
}
