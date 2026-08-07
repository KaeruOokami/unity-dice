using UnityEditor;
using UnityEngine;

namespace DiceGame.Session.Editor
{
    public static class SessionSetupMenu
    {
        const string ServicesUrl = "https://dashboard.unity3d.com/";

        [MenuItem("Dice/Session/Open Unity Gaming Services Dashboard")]
        static void OpenDashboard() {
            Application.OpenURL(ServicesUrl);
        }

        [MenuItem("Dice/Session/Select SessionController In Scene")]
        static void SelectController() {
            var controller = Object.FindObjectOfType<SessionController>();
            if (controller == null) {
                EditorUtility.DisplayDialog(
                    "Session",
                    "SessionController がシーンにありません。Game シーンの GameBootstrap に付いている想定です。",
                    "OK");
                return;
            }

            Selection.activeObject = controller.gameObject;
        }
    }
}
