using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Session
{
    public static class AttackPresetPersistence
    {
        public const int CurrentVersion = 1;

        public static string[] ListPresetNames() {
            var directory = GetDirectoryPath();
            if (!Directory.Exists(directory)) {
                return Array.Empty<string>();
            }

            var files = Directory.GetFiles(directory, "*.json");
            var names = new List<string>(files.Length);
            for (var i = 0; i < files.Length; i++) {
                var fileName = Path.GetFileNameWithoutExtension(files[i]);
                if (string.IsNullOrWhiteSpace(fileName)) {
                    continue;
                }

                names.Add(fileName);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
        }

        public static bool TrySave(string name, PlayerAttackSettingsData data, out string errorMessage) {
            if (!TryNormalizeName(name, out var normalizedName, out errorMessage)) {
                return false;
            }

            if (!data.TryValidate(out errorMessage)) {
                return false;
            }

            try {
                var directory = GetDirectoryPath();
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                var file = new AttackPresetPersistFile {
                    Version = CurrentVersion,
                    Name = normalizedName,
                    Attack = MatchSetupPersistMapper.FromAttackData(data)
                };
                var path = GetFilePath(normalizedName);
                File.WriteAllText(path, JsonUtility.ToJson(file, prettyPrint: true));
                errorMessage = null;
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TryLoad(string name, out PlayerAttackSettingsData data, out string errorMessage) {
            data = default;
            if (!TryNormalizeName(name, out var normalizedName, out errorMessage)) {
                return false;
            }

            var path = GetFilePath(normalizedName);
            if (!File.Exists(path)) {
                errorMessage = $"Attack preset '{normalizedName}' was not found.";
                return false;
            }

            try {
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<AttackPresetPersistFile>(json);
                if (file == null) {
                    errorMessage = "JSON root is null.";
                    return false;
                }

                if (file.Version != CurrentVersion) {
                    errorMessage = $"Unsupported attack preset version {file.Version}.";
                    return false;
                }

                return MatchSetupPersistMapper.TryToAttackData(file.Attack, out data, out errorMessage);
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TryDelete(string name, out string errorMessage) {
            if (!TryNormalizeName(name, out var normalizedName, out errorMessage)) {
                return false;
            }

            var path = GetFilePath(normalizedName);
            if (!File.Exists(path)) {
                errorMessage = $"Attack preset '{normalizedName}' was not found.";
                return false;
            }

            try {
                File.Delete(path);
                errorMessage = null;
                return true;
            } catch (Exception ex) {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static string GetDirectoryPath() {
            return Path.Combine(Application.persistentDataPath, SessionConstants.AttackPresetsPersistDirectory);
        }

        public static string GetFilePath(string normalizedName) {
            return Path.Combine(GetDirectoryPath(), $"{normalizedName}.json");
        }

        public static bool TryNormalizeName(string name, out string normalizedName, out string errorMessage) {
            normalizedName = null;
            if (string.IsNullOrWhiteSpace(name)) {
                errorMessage = "Preset name is empty.";
                return false;
            }

            var trimmed = name.Trim();
            if (trimmed.Length > SessionConstants.AttackPresetNameMaxLength) {
                errorMessage =
                    $"Preset name must be at most {SessionConstants.AttackPresetNameMaxLength} characters.";
                return false;
            }

            var builder = new StringBuilder(trimmed.Length);
            for (var i = 0; i < trimmed.Length; i++) {
                var c = trimmed[i];
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') {
                    builder.Append(c);
                } else if (char.IsWhiteSpace(c)) {
                    builder.Append('_');
                } else {
                    errorMessage = "Preset name may only contain letters, digits, spaces, '-' or '_'.";
                    return false;
                }
            }

            normalizedName = builder.ToString();
            if (string.IsNullOrEmpty(normalizedName)) {
                errorMessage = "Preset name is empty after normalization.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
