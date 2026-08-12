using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    /// <summary>
    /// Optional geometry exposed by discrete actions for Scene debug gizmos.
    /// </summary>
    public interface IAiDebugStepGeometry
    {
        Vector2Int DebugStepCell { get; }
        Vector2Int DebugGoalCell { get; }
    }
}
