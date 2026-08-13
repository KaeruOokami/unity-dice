using DiceGame.Grid;

namespace DiceGame.Gameplay.AI.Application
{
    /// <summary>
    /// Shared debug overlay source for rule AI and ML agent Scene gizmos.
    /// </summary>
    public interface IAiDebugOverlaySource
    {
        bool DebugGizmoEnabled { get; }
        Board DebugBoard { get; }
        AiDebugOverlaySnapshot DebugOverlay { get; }
    }
}
