using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    public static class AiDebugLog
    {
        public static bool Enabled { get; set; }

        public static void Log(string message) {
            if (!Enabled) {
                return;
            }

            Debug.Log($"[AiDebug] {message}");
        }
    }
}
