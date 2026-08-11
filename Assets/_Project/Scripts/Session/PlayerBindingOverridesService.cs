using System.Collections.Generic;
using DiceGame.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceGame.Session
{
    public static class PlayerBindingOverridesService
    {
        public static void ApplyFromDisk(params PlayerInputSettings[] settingsList) {
            if (settingsList == null || settingsList.Length == 0) {
                return;
            }

            var applied = new HashSet<InputActionAsset>();
            for (var i = 0; i < settingsList.Length; i++) {
                var asset = settingsList[i]?.InputActions;
                if (asset == null || !applied.Add(asset)) {
                    continue;
                }

                if (!PlayerBindingOverridesPersistence.TryLoadAndApply(asset, out var error) && error != null) {
                    Debug.LogError($"[PlayerBindingOverridesService] Failed to load overrides: {error}");
                }
            }
        }

        public static bool TrySave(InputActionAsset asset, out string errorMessage) {
            return PlayerBindingOverridesPersistence.TrySaveFrom(asset, out errorMessage);
        }

        public static bool TryReset(InputActionAsset asset, out string errorMessage) {
            return PlayerBindingOverridesPersistence.TryReset(asset, out errorMessage);
        }

        public static bool TryFindBindingIndex(
            InputAction action,
            string controlScheme,
            string compositePartName,
            out int bindingIndex) {
            bindingIndex = -1;
            if (action == null) {
                return false;
            }

            for (var i = 0; i < action.bindings.Count; i++) {
                var binding = action.bindings[i];
                if (binding.isComposite) {
                    continue;
                }

                if (!BindingHasGroup(binding, controlScheme)) {
                    continue;
                }

                if (!string.IsNullOrEmpty(compositePartName)) {
                    if (!binding.isPartOfComposite
                        || !string.Equals(
                            binding.name,
                            compositePartName,
                            System.StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                } else if (binding.isPartOfComposite) {
                    continue;
                }

                bindingIndex = i;
                return true;
            }

            return false;
        }

        public static bool BindingHasGroup(InputBinding binding, string group) {
            if (string.IsNullOrEmpty(binding.groups) || string.IsNullOrEmpty(group)) {
                return false;
            }

            var parts = binding.groups.Split(';');
            for (var i = 0; i < parts.Length; i++) {
                if (string.Equals(parts[i].Trim(), group, System.StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetDuplicateKeyboardBindingMessage(
            InputActionAsset asset,
            out string message) {
            message = null;
            if (asset == null) {
                return false;
            }

            var pathOwners = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            if (CollectKeyboardPathConflicts(
                    asset,
                    PlayerInputSettings.Player1ActionMap,
                    pathOwners,
                    out message)
                || CollectKeyboardPathConflicts(
                    asset,
                    PlayerInputSettings.Player2ActionMap,
                    pathOwners,
                    out message)) {
                return true;
            }

            return false;
        }

        static bool CollectKeyboardPathConflicts(
            InputActionAsset asset,
            string mapName,
            Dictionary<string, string> pathOwners,
            out string message) {
            message = null;
            var map = asset.FindActionMap(mapName, throwIfNotFound: false);
            if (map == null) {
                return false;
            }

            for (var actionIndex = 0; actionIndex < map.actions.Count; actionIndex++) {
                var action = map.actions[actionIndex];
                for (var bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++) {
                    var binding = action.bindings[bindingIndex];
                    if (binding.isComposite
                        || !BindingHasGroup(binding, PlayerInputSettings.KeyboardScheme)) {
                        continue;
                    }

                    var path = action.GetBindingDisplayString(
                        bindingIndex,
                        InputBinding.DisplayStringOptions.DontOmitDevice);
                    if (string.IsNullOrWhiteSpace(path) || path == "null") {
                        continue;
                    }

                    var owner = $"{mapName}/{action.name}";
                    if (!string.IsNullOrEmpty(binding.name)) {
                        owner = $"{owner}.{binding.name}";
                    }

                    if (pathOwners.TryGetValue(path, out var existingOwner)) {
                        message = $"Key '{path}' is used by both {existingOwner} and {owner}.";
                        return true;
                    }

                    pathOwners[path] = owner;
                }
            }

            return false;
        }
    }
}
