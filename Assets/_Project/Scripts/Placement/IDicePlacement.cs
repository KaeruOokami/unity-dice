using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Placement
{
    public interface IDicePlacement
    {
        bool CanPlaceBottomDiceAt(Vector2Int gridPos);
        bool CanPlaceTopDiceAt(Vector2Int gridPos);
        bool HasTopAt(Vector2Int gridPos);
        bool HasBottomAt(Vector2Int gridPos);
    }
}
