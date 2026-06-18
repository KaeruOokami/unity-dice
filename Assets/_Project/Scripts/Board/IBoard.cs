using UnityEngine;

namespace DiceGame.Grid
{
    public interface IBoard
    {
        int Width { get; }
        int Height { get; }
        float CellSize { get; }
        bool IsInside(Vector2Int gridPos);
        bool CanEnter(Vector2Int gridPos);
        Vector3 GridToWorld(Vector2Int gridPos);
        Vector2Int? DicePosition { get; }
        void SetDicePosition(Vector2Int? gridPos);
    }
}
