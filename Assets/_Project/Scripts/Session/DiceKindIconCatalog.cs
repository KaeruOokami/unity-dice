using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    /// <summary>
    /// Title/settings UI: Kind 表示用に DiceCatalog mesh からアイコンを生成・保持する。
    /// 出目 1〜6 を一定間隔で切り替えて表示する。
    /// </summary>
    public sealed class DiceKindIconCatalog : IDisposable
    {
        struct ImageBinding
        {
            public Image Image;
            public DiceKind Kind;
        }

        readonly Dictionary<DiceKind, Sprite[]> iconsByKind = new();
        readonly List<ImageBinding> imageBindings = new();
        readonly List<TMP_Dropdown> dropdownBindings = new();
        DiceIconGenerator generator;
        AttackQueueSettings ownedSettings;
        float iconSize = 32f;
        int currentPip = DiceIconGenerator.MinPip;
        float cycleTimer;
        bool disposed;

        public float IconSize => iconSize;
        public int CurrentPip => currentPip;

        public IEnumerator WarmRoutine(Transform parent, MatchSetupPresetRegistry registry) {
            if (parent == null) {
                Debug.LogError("[DiceKindIconCatalog] parent is required.");
                yield break;
            }

            DisposeGeneratorOnly();
            iconsByKind.Clear();
            imageBindings.Clear();
            dropdownBindings.Clear();
            disposed = false;
            currentPip = DiceIconGenerator.MinPip;
            cycleTimer = 0f;

            ownedSettings = AttackQueueSettings.CreateRuntimeFallback();
            iconSize = ownedSettings.IconSize;
            generator = new DiceIconGenerator(parent, ownedSettings);

            var kinds = (DiceKind[])Enum.GetValues(typeof(DiceKind));
            var perFrame = ownedSettings.IconPrewarmPerFrame;
            var warmedThisFrame = 0;

            for (var i = 0; i < kinds.Length; i++) {
                if (disposed) {
                    yield break;
                }

                var kind = kinds[i];
                if (!TryResolveMeshPrefab(registry, kind, out var meshPrefab)) {
                    continue;
                }

                var sprites = new Sprite[DiceIconGenerator.MaxPip + 1];
                var warmedAny = false;
                for (var pip = DiceIconGenerator.MinPip; pip <= DiceIconGenerator.MaxPip; pip++) {
                    if (disposed) {
                        yield break;
                    }

                    if (!generator.WarmSprite(meshPrefab, pip)) {
                        Debug.LogError(
                            $"[DiceKindIconCatalog] Failed to warm icon for {kind} pip={pip} mesh='{meshPrefab.name}'.");
                        continue;
                    }

                    if (!generator.TryGetCachedSprite(meshPrefab, pip, out var sprite) || sprite == null) {
                        Debug.LogError(
                            $"[DiceKindIconCatalog] Cached icon missing for {kind} pip={pip}.");
                        continue;
                    }

                    sprites[pip] = sprite;
                    warmedAny = true;
                    warmedThisFrame++;
                    if (warmedThisFrame < perFrame) {
                        continue;
                    }

                    warmedThisFrame = 0;
                    yield return null;
                }

                if (warmedAny) {
                    iconsByKind[kind] = sprites;
                }
            }
        }

        public bool TryGet(DiceKind kind, int pip, out Sprite sprite) {
            sprite = null;
            if (disposed
                || pip < DiceIconGenerator.MinPip
                || pip > DiceIconGenerator.MaxPip
                || !iconsByKind.TryGetValue(kind, out var sprites)
                || sprites == null) {
                return false;
            }

            sprite = sprites[pip];
            return sprite != null;
        }

        public bool TryGet(DiceKind kind, out Sprite sprite) {
            return TryGet(kind, currentPip, out sprite);
        }

        public void RegisterImage(Image image, DiceKind kind) {
            if (disposed || image == null) {
                return;
            }

            imageBindings.Add(new ImageBinding { Image = image, Kind = kind });
            if (TryGet(kind, currentPip, out var sprite)) {
                image.sprite = sprite;
            }
        }

        public void RegisterDropdown(TMP_Dropdown dropdown) {
            if (disposed || dropdown == null) {
                return;
            }

            dropdownBindings.Add(dropdown);
            ApplyPipToDropdown(dropdown, currentPip);
        }

        public void Tick(float deltaTime) {
            if (disposed || iconsByKind.Count == 0) {
                return;
            }

            PruneBindings();
            cycleTimer += Mathf.Max(0f, deltaTime);
            if (cycleTimer < SessionConstants.DiceKindIconPipCycleSeconds) {
                return;
            }

            cycleTimer = 0f;
            currentPip = currentPip >= DiceIconGenerator.MaxPip
                ? DiceIconGenerator.MinPip
                : currentPip + 1;
            ApplyCurrentPip();
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            iconsByKind.Clear();
            imageBindings.Clear();
            dropdownBindings.Clear();
            DisposeGeneratorOnly();
        }

        void ApplyCurrentPip() {
            for (var i = 0; i < imageBindings.Count; i++) {
                var binding = imageBindings[i];
                if (binding.Image == null) {
                    continue;
                }

                if (TryGet(binding.Kind, currentPip, out var sprite)) {
                    binding.Image.sprite = sprite;
                }
            }

            for (var i = 0; i < dropdownBindings.Count; i++) {
                ApplyPipToDropdown(dropdownBindings[i], currentPip);
            }
        }

        void ApplyPipToDropdown(TMP_Dropdown dropdown, int pip) {
            if (dropdown == null || dropdown.options == null) {
                return;
            }

            var kinds = (DiceKind[])Enum.GetValues(typeof(DiceKind));
            var options = dropdown.options;
            var count = Mathf.Min(options.Count, kinds.Length);
            for (var i = 0; i < count; i++) {
                if (TryGet(kinds[i], pip, out var sprite)) {
                    options[i].image = sprite;
                }
            }

            dropdown.RefreshShownValue();
        }

        void PruneBindings() {
            for (var i = imageBindings.Count - 1; i >= 0; i--) {
                if (imageBindings[i].Image == null) {
                    imageBindings.RemoveAt(i);
                }
            }

            for (var i = dropdownBindings.Count - 1; i >= 0; i--) {
                if (dropdownBindings[i] == null) {
                    dropdownBindings.RemoveAt(i);
                }
            }
        }

        void DisposeGeneratorOnly() {
            generator?.Dispose();
            generator = null;
            if (ownedSettings != null) {
                UnityEngine.Object.Destroy(ownedSettings);
                ownedSettings = null;
            }
        }

        static bool TryResolveMeshPrefab(
            MatchSetupPresetRegistry registry,
            DiceKind kind,
            out GameObject meshPrefab) {
            meshPrefab = null;
            if (registry == null) {
                return false;
            }

            if (TryGetFromCatalog(registry.DefaultSharedCatalog, kind, out meshPrefab)) {
                return true;
            }

            var presets = registry.CatalogPresets;
            if (presets == null) {
                return false;
            }

            for (var i = 0; i < presets.Length; i++) {
                if (TryGetFromCatalog(presets[i], kind, out meshPrefab)) {
                    return true;
                }
            }

            return false;
        }

        static bool TryGetFromCatalog(DiceCatalog catalog, DiceKind kind, out GameObject meshPrefab) {
            meshPrefab = null;
            return catalog != null && catalog.TryGetMeshPrefab(kind, out meshPrefab);
        }
    }
}
