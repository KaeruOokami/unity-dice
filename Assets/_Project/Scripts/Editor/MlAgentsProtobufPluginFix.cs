using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceGame.Editor
{
    /// <summary>
    /// Player builds fail with CS0400 on Google.Protobuf when Sentis' Editor copy of
    /// Google.Protobuf_Packed.dll shadows the ML-Agents plugin. Keep Sentis Editor-only
    /// and ML-Agents Standalone-only so each compilation target has exactly one DLL.
    /// </summary>
    [InitializeOnLoad]
    public sealed class MlAgentsProtobufPluginFix : IPreprocessBuildWithReport
    {
        const string PluginFileName = "Google.Protobuf_Packed.dll";
        const string MlAgentsPackageMarker = "/com.unity.ml-agents/";
        const string InferencePackageMarker = "/com.unity.ai.inference/";

        public int callbackOrder => -100;

        static MlAgentsProtobufPluginFix() {
            EditorApplication.delayCall += Apply;
        }

        [MenuItem("Dice/ML-Agents/Fix Protobuf Plugins")]
        public static void Apply() {
            var guids = AssetDatabase.FindAssets("Google.Protobuf_Packed");
            var changed = false;

            for (var i = 0; i < guids.Length; i++) {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path == null || !path.EndsWith(PluginFileName)) {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as PluginImporter;
                if (importer == null) {
                    continue;
                }

                if (path.Contains(MlAgentsPackageMarker)) {
                    changed |= ApplyMlAgentsPlayerPlugin(importer);
                    continue;
                }

                if (path.Contains(InferencePackageMarker)) {
                    changed |= ApplySentisEditorPlugin(importer);
                }
            }

            if (changed) {
                AssetDatabase.SaveAssets();
                Debug.Log("MlAgentsProtobufPluginFix: updated Google.Protobuf_Packed.dll platform settings.");
            }
        }

        public void OnPreprocessBuild(BuildReport report) {
            Apply();
        }

        static bool ApplyMlAgentsPlayerPlugin(PluginImporter importer) {
            var changed = false;
            changed |= SetExplicitlyReferenced(importer, true);
            changed |= SetCompatible(importer.GetCompatibleWithAnyPlatform(), false, importer.SetCompatibleWithAnyPlatform);
            changed |= SetCompatible(importer.GetCompatibleWithEditor(), false, importer.SetCompatibleWithEditor);
            changed |= SetPlatform(importer, BuildTarget.StandaloneWindows, true);
            changed |= SetPlatform(importer, BuildTarget.StandaloneWindows64, true);
            changed |= SetPlatform(importer, BuildTarget.StandaloneLinux64, true);
            changed |= SetPlatform(importer, BuildTarget.StandaloneOSX, true);
            if (changed) {
                importer.SaveAndReimport();
            }

            return changed;
        }

        static bool ApplySentisEditorPlugin(PluginImporter importer) {
            var changed = false;
            changed |= SetExplicitlyReferenced(importer, true);
            changed |= SetCompatible(importer.GetCompatibleWithAnyPlatform(), false, importer.SetCompatibleWithAnyPlatform);
            changed |= SetCompatible(importer.GetCompatibleWithEditor(), true, importer.SetCompatibleWithEditor);
            changed |= SetPlatform(importer, BuildTarget.StandaloneWindows, false);
            changed |= SetPlatform(importer, BuildTarget.StandaloneWindows64, false);
            changed |= SetPlatform(importer, BuildTarget.StandaloneLinux64, false);
            changed |= SetPlatform(importer, BuildTarget.StandaloneOSX, false);
            if (changed) {
                importer.SaveAndReimport();
            }

            return changed;
        }

        static bool SetExplicitlyReferenced(PluginImporter importer, bool value) {
            var serialized = new SerializedObject(importer);
            var property = serialized.FindProperty("isExplicitlyReferenced");
            if (property == null || property.boolValue == value) {
                return false;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static bool SetCompatible(bool current, bool desired, System.Action<bool> setter) {
            if (current == desired) {
                return false;
            }

            setter(desired);
            return true;
        }

        static bool SetPlatform(PluginImporter importer, BuildTarget target, bool enabled) {
            if (importer.GetCompatibleWithPlatform(target) == enabled) {
                return false;
            }

            importer.SetCompatibleWithPlatform(target, enabled);
            return true;
        }
    }
}
