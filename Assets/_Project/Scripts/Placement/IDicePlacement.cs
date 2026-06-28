using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Placement
{
    public interface IDicePlacement
    {
        bool CanPlaceBottomDiceAt(Vector2Int gridPos);
        bool CanPlaceTopDiceAt(Vector2Int gridPos);
        bool CanDiceRollInto(Vector2Int gridPos);
        bool CanParallelRollLandAt(Vector2Int gridPos, DiceStackTier tier);
    }
}
