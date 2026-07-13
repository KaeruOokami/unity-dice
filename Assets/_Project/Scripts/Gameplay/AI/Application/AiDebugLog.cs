using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    public static class AiDebugLog
    {
        public static void Log(string message) {
            Debug.Log($"[AiDebug] {message}");
        }
    }
}
