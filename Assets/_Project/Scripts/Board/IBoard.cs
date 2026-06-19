using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Grid
{
    public interface IBoard
    {
        int Width { get; }
        int Height { get; }
        float CellSize { get; }
        bool IsInside(Vector2Int gridPos);
        bool HasBottomDiceAt(Vector2Int gridPos);
        bool HasTopDiceAt(Vector2Int gridPos);
        bool HasDiceAt(Vector2Int gridPos);
        bool CanPlaceBottomDiceAt(Vector2Int gridPos);
        bool CanPlaceTopDiceAt(Vector2Int gridPos);
        bool CanDiceRollInto(Vector2Int gridPos);
        float FloorSurfaceWorldY { get; }
        Vector3 GridToWorld(Vector2Int gridPos);
        Vector2Int WorldToGrid(Vector3 worldPosition);
        void RegisterDice(Vector2Int gridPos, DiceStackTier tier);
        void UnregisterDice(Vector2Int gridPos, DiceStackTier tier);
        void MoveDice(Vector2Int from, Vector2Int to, DiceStackTier tier);
    }
}
