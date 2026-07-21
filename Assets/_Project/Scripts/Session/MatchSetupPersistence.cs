using System;
using System.IO;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Session
{
    public static class MatchSetupPersistence
    {
        public const int CurrentVersion = 1;

        public static MatchSetupSnapshot LoadOrCreate(GameMode mode, MatchSetupPresetRegistry registry) {
            if (registry == null) {
                Debug.LogError("[MatchSetupPersistence] Preset registry is not assigned.");
                return null;
            }

            var path = GetFilePath(mode);
            if (File.Exists(path)) {
                if (TryLoad(path, registry, out var loaded, out var loadError)) {
                    return loaded;
                }

                Debug.LogError(
                    $"[MatchSetupPersistence] Failed to load '{path}': {loadError}. Falling back to SO defaults.");
            }

            var created = registry.CreateDefaultSnapshot(mode);
            if (!TrySave(created, registry, out var saveError)) {
                Debug.LogError($"[MatchSetupPersistence] Failed to create initial JSON: {saveError}");
            }

            return created;
        }

        public static bool TrySave(
            MatchSetupSnapshot snapshot,
            MatchSetupPresetRegistry registry,
            out string errorMessage) {
            if (snapshot == null) {
                errorMessage = "MatchSetupPersistence: Snapshot is null.";
                return false;
            }

            if (registry == null) {
                errorMessage = "MatchSetupPersistence: Preset registry is not assigned.";
                return false;
            }

            try {
                var directory = GetDirectoryPath();
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                var payload = MatchSetupNetworkCodec.ToPayload(snapshot, registry);
                var file = MatchSetupPersistMapper.FromNetworkPayload(payload);
                var json = JsonUtility.ToJson(file, prettyPrint: true);
                File.WriteAllText(GetFilePath(snapshot.GameMode), json);
                errorMessage = null;
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static string GetFilePath(GameMode mode) {
            return Path.Combine(GetDirectoryPath(), $"match_setup_{mode}.json");
        }

        static string GetDirectoryPath() {
            return Path.Combine(Application.persistentDataPath, OnlineSessionConstants.MatchSetupPersistDirectory);
        }

        static bool TryLoad(
            string path,
            MatchSetupPresetRegistry registry,
            out MatchSetupSnapshot snapshot,
            out string errorMessage) {
            snapshot = null;
            try {
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<MatchSetupPersistFile>(json);
                if (file == null) {
                    errorMessage = "JSON root is null.";
                    return false;
                }

                if (file.Version != CurrentVersion) {
                    errorMessage = $"Unsupported persist version {file.Version}.";
                    return false;
                }

                var payload = MatchSetupPersistMapper.ToNetworkPayload(file);
                return MatchSetupNetworkCodec.TryFromPayload(payload, registry, out snapshot, out errorMessage);
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
