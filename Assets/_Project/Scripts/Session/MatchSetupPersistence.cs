using System;
using System.IO;
using System.Text;
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

            var path = GetLocalFilePath(mode);
            if (File.Exists(path)) {
                if (TryLoad(path, registry, out var loaded, out var loadError)) {
                    loaded.NormalizeVersusSharedInitialDiceCount();
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

        public static MatchSetupSnapshot LoadOrCreateOnline(
            GameMode mode,
            string remotePeerPlayerId,
            MatchSetupPresetRegistry registry) {
            if (registry == null) {
                Debug.LogError("[MatchSetupPersistence] Preset registry is not assigned.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(remotePeerPlayerId)) {
                Debug.LogError("[MatchSetupPersistence] Online peer player id is empty.");
                return registry.CreateDefaultSnapshot(mode);
            }

            var path = GetOnlineFilePath(mode, remotePeerPlayerId);
            if (File.Exists(path)) {
                if (TryLoad(path, registry, out var loaded, out var loadError)) {
                    loaded.NormalizeVersusSharedInitialDiceCount();
                    return loaded;
                }

                Debug.LogError(
                    $"[MatchSetupPersistence] Failed to load '{path}': {loadError}. Falling back to SO defaults.");
            }

            var created = registry.CreateDefaultSnapshot(mode);
            created.GameMode = mode;
            if (!TrySaveOnline(created, remotePeerPlayerId, registry, out var saveError)) {
                Debug.LogError($"[MatchSetupPersistence] Failed to create initial online JSON: {saveError}");
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

            return TryWrite(GetLocalFilePath(snapshot.GameMode), snapshot, registry, out errorMessage);
        }

        public static bool TrySaveOnline(
            MatchSetupSnapshot snapshot,
            string remotePeerPlayerId,
            MatchSetupPresetRegistry registry,
            out string errorMessage) {
            if (snapshot == null) {
                errorMessage = "MatchSetupPersistence: Snapshot is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(remotePeerPlayerId)) {
                errorMessage = "MatchSetupPersistence: Online peer player id is empty.";
                return false;
            }

            return TryWrite(
                GetOnlineFilePath(snapshot.GameMode, remotePeerPlayerId),
                snapshot,
                registry,
                out errorMessage);
        }

        public static string GetLocalFilePath(GameMode mode) {
            return Path.Combine(GetLocalDirectoryPath(), $"match_setup_{mode}.json");
        }

        public static string GetOnlineFilePath(GameMode mode, string remotePeerPlayerId) {
            var safePeerId = SanitizeFileToken(remotePeerPlayerId);
            return Path.Combine(
                GetOnlineDirectoryPath(),
                $"online_{mode}_peer_{safePeerId}.json");
        }

        static bool TryWrite(
            string path,
            MatchSetupSnapshot snapshot,
            MatchSetupPresetRegistry registry,
            out string errorMessage) {
            if (registry == null) {
                errorMessage = "MatchSetupPersistence: Preset registry is not assigned.";
                return false;
            }

            try {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                var payload = MatchSetupNetworkCodec.ToPayload(snapshot, registry);
                var file = MatchSetupPersistMapper.FromNetworkPayload(payload);
                var json = JsonUtility.ToJson(file, prettyPrint: true);
                File.WriteAllText(path, json);
                errorMessage = null;
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        static string GetLocalDirectoryPath() {
            return Path.Combine(Application.persistentDataPath, OnlineSessionConstants.MatchSetupPersistDirectory);
        }

        static string GetOnlineDirectoryPath() {
            return Path.Combine(
                Application.persistentDataPath,
                OnlineSessionConstants.MatchSetupPersistDirectory,
                OnlineSessionConstants.MatchSetupOnlinePersistDirectory);
        }

        static string SanitizeFileToken(string value) {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++) {
                var c = value[i];
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') {
                    builder.Append(c);
                } else {
                    builder.Append('_');
                }
            }

            return builder.Length > 0 ? builder.ToString() : "unknown";
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
