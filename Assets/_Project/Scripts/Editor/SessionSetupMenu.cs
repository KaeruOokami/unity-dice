using UnityEditor;
using UnityEngine;

namespace DiceGame.Session.Editor
{
    public static class OnlineSessionSetupMenu
    {
        const string ServicesUrl = "https://dashboard.unity3d.com/";

        [MenuItem("Dice/Online/Open Unity Gaming Services Dashboard")]
        static void OpenDashboard() {
            Application.OpenURL(ServicesUrl);
        }

        [MenuItem("Dice/Online/Select OnlineSessionController In Scene")]
        static void SelectController() {
            var controller = Object.FindObjectOfType<OnlineSessionController>();
            if (controller == null) {
                EditorUtility.DisplayDialog(
                    "Online Session",
                    "OnlineSessionController がシーンにありません。Game シーンの GameBootstrap に付いている想定です。",
                    "OK");
                return;
            }

            Selection.activeObject = controller.gameObject;
        }
    }
}
