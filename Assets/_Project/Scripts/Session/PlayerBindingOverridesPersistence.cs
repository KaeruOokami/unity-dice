using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceGame.Session
{
    public static class PlayerBindingOverridesPersistence
    {
        public const int CurrentVersion = 1;

        public static bool TryLoadAndApply(InputActionAsset asset, out string errorMessage) {
            if (asset == null) {
                errorMessage = "PlayerBindingOverridesPersistence: InputActionAsset is null.";
                return false;
            }

            var path = GetFilePath();
            if (!File.Exists(path)) {
                errorMessage = null;
                return true;
            }

            try {
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<PlayerBindingOverridesPersistFile>(json);
                if (file == null) {
                    errorMessage = "JSON root is null.";
                    return false;
                }

                if (file.Version != CurrentVersion) {
                    errorMessage = $"Unsupported persist version {file.Version}.";
                    return false;
                }

                asset.RemoveAllBindingOverrides();
                if (!string.IsNullOrWhiteSpace(file.OverridesJson)) {
                    asset.LoadBindingOverridesFromJson(file.OverridesJson);
                }

                errorMessage = null;
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TrySaveFrom(InputActionAsset asset, out string errorMessage) {
            if (asset == null) {
                errorMessage = "PlayerBindingOverridesPersistence: InputActionAsset is null.";
                return false;
            }

            try {
                var path = GetFilePath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                var file = new PlayerBindingOverridesPersistFile {
                    Version = CurrentVersion,
                    OverridesJson = asset.SaveBindingOverridesAsJson()
                };
                File.WriteAllText(path, JsonUtility.ToJson(file, prettyPrint: true));
                errorMessage = null;
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TryReset(InputActionAsset asset, out string errorMessage) {
            if (asset == null) {
                errorMessage = "PlayerBindingOverridesPersistence: InputActionAsset is null.";
                return false;
            }

            try {
                asset.RemoveAllBindingOverrides();
                var path = GetFilePath();
                if (File.Exists(path)) {
                    File.Delete(path);
                }

                errorMessage = null;
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static string GetFilePath() {
            return Path.Combine(
                Application.persistentDataPath,
                SessionConstants.InputBindingsPersistDirectory,
                SessionConstants.InputBindingsPersistFileName);
        }
    }
}
