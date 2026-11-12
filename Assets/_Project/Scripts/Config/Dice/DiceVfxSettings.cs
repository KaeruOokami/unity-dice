using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceVfxSettings", menuName = "Dice/Dice/Vfx Settings")]
    public class DiceVfxSettings : ScriptableObject
    {
        const int AttackSendTierCount = 3;
        const int AttackSendMinCount = 1;

        [Header("Spawn Appear (Lightning)")]
        [FormerlySerializedAs("prefab")]
        [SerializeField] VisualEffect spawnAppearPrefab;
        [FormerlySerializedAs("startHeight")]
        [SerializeField] float spawnAppearStartHeight = 20f;
        [FormerlySerializedAs("fadeOutDuration")]
        [FormerlySerializedAs("destroyDelay")]
        [SerializeField] float spawnAppearFadeOutDuration = 1f;

        [Header("Fire")]
        [SerializeField] VisualEffect firePrefab;
        [SerializeField] float fireHeightOffset;
        [SerializeField] float fireFadeOutDuration = 1f;

        [Header("Attack Send (Erase → Queue)")]
        [SerializeField] VisualEffect attackSendTier1Prefab;
        [SerializeField] VisualEffect attackSendTier2Prefab;
        [SerializeField] VisualEffect attackSendTier3Prefab;
        [Min(0.01f)]
        [SerializeField] float attackSendLifetime = 2f;
        [Min(0f)]
        [SerializeField] float attackSendDestroyGrace = 0.5f;

        public VisualEffect SpawnAppearPrefab => spawnAppearPrefab;
        public float SpawnAppearStartHeight => spawnAppearStartHeight;
        public float SpawnAppearFadeOutDuration => spawnAppearFadeOutDuration;

        public VisualEffect FirePrefab => firePrefab;
        public float FireHeightOffset => fireHeightOffset;
        public float FireFadeOutDuration => fireFadeOutDuration;

        public float AttackSendLifetime => Mathf.Max(0.01f, attackSendLifetime);
        public float AttackSendDestroyGrace => Mathf.Max(0f, attackSendDestroyGrace);

        /// <summary>
        /// Picks tier by sent count within [minCount, maxCount] (three equal spans).
        /// Example: 1..3 → thresholds 1 / 2 / 3; 1..5 → 1 / 3 / 5.
        /// </summary>
        public VisualEffect ResolveAttackSendPrefab(int sentCount, int minCount, int maxCount) {
            var tier = ResolveAttackSendTier(sentCount, minCount, maxCount);
            if (tier >= 3) {
                return attackSendTier3Prefab != null ? attackSendTier3Prefab : ResolveLowerTierPrefab(2);
            }

            if (tier >= 2) {
                return attackSendTier2Prefab != null ? attackSendTier2Prefab : ResolveLowerTierPrefab(1);
            }

            return attackSendTier1Prefab;
        }

        public static int ResolveAttackSendTier(int sentCount, int minCount, int maxCount) {
            var min = Mathf.Max(AttackSendMinCount, minCount);
            var max = Mathf.Max(min, maxCount);
            var count = Mathf.Clamp(sentCount, min, max);
            if (max <= min) {
                return 1;
            }

            var span = max - min;
            var tier1End = min + span / (float)AttackSendTierCount;
            var tier2End = min + span * 2f / AttackSendTierCount;
            if (count <= tier1End) {
                return 1;
            }

            if (count <= tier2End) {
                return 2;
            }

            return 3;
        }

        VisualEffect ResolveLowerTierPrefab(int fromTier) {
            if (fromTier >= 2 && attackSendTier2Prefab != null) {
                return attackSendTier2Prefab;
            }

            return attackSendTier1Prefab;
        }
    }
}
