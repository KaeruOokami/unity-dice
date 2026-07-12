using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DiceGame.View
{
    sealed class DiceIconGenerator : IDisposable
    {
        readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly int MeshPrefabId;
            public readonly int Pip;

            public CacheKey(int meshPrefabId, int pip) {
                MeshPrefabId = meshPrefabId;
                Pip = pip;
            }

            public bool Equals(CacheKey other) {
                return MeshPrefabId == other.MeshPrefabId && Pip == other.Pip;
            }

            public override bool Equals(object obj) {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode() {
                return HashCode.Combine(MeshPrefabId, Pip);
            }
        }

        readonly AttackQueueUiSettings settings;
        readonly Dictionary<CacheKey, Sprite> cache = new();
        readonly Transform previewRoot;
        readonly Camera previewCamera;
        readonly RenderTexture renderTexture;
        readonly int previewLayer;
        bool disposed;

        public DiceIconGenerator(Transform parent, AttackQueueUiSettings targetSettings) {
            if (targetSettings == null) {
                throw new ArgumentNullException(nameof(targetSettings));
            }

            settings = targetSettings;
            previewLayer = ResolvePreviewLayer(settings.PreviewLayerName);
            if (previewLayer < 0) {
                previewLayer = 0;
            }

            previewRoot = new GameObject("DiceIconPreviewRoot").transform;
            previewRoot.SetParent(parent, false);
            previewRoot.position = new Vector3(10000f, 10000f, 10000f);

            var cameraObject = new GameObject("DiceIconPreviewCamera");
            cameraObject.transform.SetParent(previewRoot, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << previewLayer;
            previewCamera.enabled = false;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 10f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderShadows = false;

            var lightObject = new GameObject("DiceIconPreviewLight");
            lightObject.transform.SetParent(previewRoot, false);
            var previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = settings.PreviewLightIntensity;
            previewLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var resolution = settings.IconResolution;
            renderTexture = new RenderTexture(
                resolution,
                resolution,
                24,
                RenderTextureFormat.ARGB32) {
                antiAliasing = 4
            };
            previewCamera.targetTexture = renderTexture;
        }

        public bool TryGetSprite(GameObject meshPrefab, int pip, out Sprite sprite) {
            sprite = null;
            if (disposed || meshPrefab == null || pip is < 1 or > 6) {
                return false;
            }

            var key = new CacheKey(meshPrefab.GetInstanceID(), pip);
            if (cache.TryGetValue(key, out sprite)) {
                return sprite != null;
            }

            sprite = RenderSprite(meshPrefab, pip);
            if (sprite != null) {
                cache[key] = sprite;
            }

            return sprite != null;
        }

        Sprite RenderSprite(GameObject meshPrefab, int pip) {
            var instance = UnityEngine.Object.Instantiate(meshPrefab, previewRoot);
            SetLayerRecursive(instance, previewLayer);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = DiceOrientationMapper.ToRotation(
                DiceOrientation.CreateWithTopFace(pip));
            instance.transform.localScale = Vector3.one;

            var bounds = CalculateRendererBounds(instance);
            if (bounds.size.sqrMagnitude <= 0f) {
                UnityEngine.Object.Destroy(instance);
                Debug.LogError($"DiceIconGenerator: Mesh prefab '{meshPrefab.name}' has no render bounds.");
                return null;
            }

            var halfExtent = Mathf.Max(bounds.extents.x, bounds.extents.z) * settings.BoundsPadding;
            previewCamera.orthographicSize = halfExtent;
            previewCamera.transform.position = bounds.center + Vector3.up * 2f;
            previewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            previewCamera.Render();

            var resolution = settings.IconResolution;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var previousTarget = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0);
            texture.Apply();
            RenderTexture.active = previousTarget;

            UnityEngine.Object.Destroy(instance);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                settings.IconPixelsPerUnit);
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            foreach (var pair in cache) {
                if (pair.Value == null) {
                    continue;
                }

                if (pair.Value.texture != null) {
                    UnityEngine.Object.Destroy(pair.Value.texture);
                }

                UnityEngine.Object.Destroy(pair.Value);
            }

            cache.Clear();

            if (renderTexture != null) {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }

            if (previewRoot != null) {
                UnityEngine.Object.Destroy(previewRoot.gameObject);
            }
        }

        static int ResolvePreviewLayer(string layerName) {
            if (string.IsNullOrWhiteSpace(layerName)) {
                Debug.LogError("DiceIconGenerator: Preview layer name is not configured.");
                return 0;
            }

            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) {
                Debug.LogError(
                    $"DiceIconGenerator: Layer '{layerName}' was not found. Configure it in Project Settings > Tags and Layers.");
            }

            return layer;
        }

        static Bounds CalculateRendererBounds(GameObject root) {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) {
                return default;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        static void SetLayerRecursive(GameObject root, int layer) {
            root.layer = layer;
            var transform = root.transform;
            for (var i = 0; i < transform.childCount; i++) {
                SetLayerRecursive(transform.GetChild(i).gameObject, layer);
            }
        }
    }
}
