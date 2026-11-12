using DiceGame.Config;
using UnityEngine;

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

        public static bool TryPlay(
            DiceVfxSettings settings,
            int sentCount,
            int minSendCount,
            int maxSendCount,
            Vector3 startWorldOnCameraPlane,
            Vector3 endWorldOnCameraPlane) {
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
            // before Start/End are set.
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
            instance.gameObject.SetActive(true);
            instance.Play();

            Object.Destroy(
                instance.gameObject,
                settings.AttackSendLifetime + settings.AttackSendDestroyGrace);
            return true;
        }
    }
}
