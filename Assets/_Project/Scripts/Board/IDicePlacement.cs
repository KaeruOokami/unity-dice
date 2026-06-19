using UnityEngine;

namespace DiceGame.Grid
{
    public interface IDicePlacement
    {
        bool CanPlaceBottomDiceAt(Vector2Int gridPos);
        bool CanPlaceTopDiceAt(Vector2Int gridPos);
        bool CanDiceRollInto(Vector2Int gridPos);
    }
}
