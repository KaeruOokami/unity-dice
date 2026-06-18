using UnityEngine;
using UnityEditor;

public class SelectScene : EditorWindow
{
    private const string DefaultSceneSearchFolder = "Assets/_Project";

    private string[] scenePaths;

    [MenuItem("Window/SelectScene")]
    public static void OpenWindow() {
        EditorWindow.GetWindow<SelectScene>();
    }

    private void OnEnable() {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { DefaultSceneSearchFolder });
        scenePaths = new string[guids.Length];
        for (var i = 0; i < guids.Length; ++i) {
            scenePaths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
        }
        Repaint();
    }

    private void OnGUI() {
        foreach (var scenePath in scenePaths) {
            int startwordPosition = scenePath.LastIndexOf ("/") + 1;
            if(GUILayout.Button(scenePath.Substring(startwordPosition, scenePath.Length - startwordPosition - 6) + "シーン")) {
                EditorApplication.OpenScene(scenePath);
            }
        }
    }
}
