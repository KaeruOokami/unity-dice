using DiceGame.Config;
using UnityEngine;
using UnityEngine.VFX;

namespace DiceGame.View
{
    /// <summary>
    /// Plays one attack-send VFX whose Start/End properties are World-space.
    /// Tier is chosen from sent dice count within the attack's sendable count range.
    /// </summary>
    public static class AttackSendVfxPlayer
    {
        static readonly int StartPositionId = Shader.PropertyToID("Start Position");
        static readonly int EndPositionId = Shader.PropertyToID("End Position");
        static readonly int GradientId = Shader.PropertyToID("Gradient");

        public static bool TryPlay(
            DiceVfxSettings settings,
            int sentCount,
            int minSendCount,
            int maxSendCount,
            Vector3 startWorldOnCameraPlane,
            Vector3 endWorldOnCameraPlane,
            Color emissionColor) {
            if (settings == null) {
                Debug.LogError("AttackSendVfxPlayer: DiceVfxSettings is null.");
                return false;
            }

            var prefab = settings.ResolveAttackSendPrefab(sentCount, minSendCount, maxSendCount);
            if (prefab == null) {
                Debug.LogError(
                    "AttackSendVfxPlayer: No attack-send VFX prefab for " +
                    $"count={sentCount} range=[{minSendCount},{maxSendCount}]. " +
                    "Assign Attack Send Tier prefabs on DiceVfxSettings.");
                return false;
            }

            // Instantiate under an inactive host so VisualEffect does not send Initial OnPlay
            // before Start/End/Gradient are set.
            var inactiveHost = new GameObject(nameof(AttackSendVfxPlayer));
            inactiveHost.hideFlags = HideFlags.HideAndDontSave;
            inactiveHost.SetActive(false);
            var instance = Object.Instantiate(prefab, inactiveHost.transform);
            instance.transform.SetParent(null, false);
            Object.Destroy(inactiveHost);

            var facing = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
            instance.transform.SetPositionAndRotation(startWorldOnCameraPlane, facing);
            instance.SetVector3(StartPositionId, startWorldOnCameraPlane);
            instance.SetVector3(EndPositionId, endWorldOnCameraPlane);
            ApplyEmissionGradient(instance, emissionColor);
            instance.gameObject.SetActive(true);
            instance.Play();

            Object.Destroy(
                instance.gameObject,
                settings.AttackSendLifetime + settings.AttackSendDestroyGrace);
            return true;
        }

        static void ApplyEmissionGradient(VisualEffect instance, Color emissionColor) {
            if (instance == null || !instance.HasGradient(GradientId)) {
                return;
            }

            instance.SetGradient(GradientId, CreateEmissionGradient(emissionColor));
        }

        static Gradient CreateEmissionGradient(Color emissionColor) {
            var opaque = new Color(emissionColor.r, emissionColor.g, emissionColor.b, 1f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(opaque, 0f),
                    new GradientColorKey(opaque, 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.96f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}
