using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceVfxSettings", menuName = "Dice/Dice Vfx Settings")]
    public class DiceVfxSettings : ScriptableObject
    {
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

        public VisualEffect SpawnAppearPrefab => spawnAppearPrefab;
        public float SpawnAppearStartHeight => spawnAppearStartHeight;
        public float SpawnAppearFadeOutDuration => spawnAppearFadeOutDuration;

        public VisualEffect FirePrefab => firePrefab;
        public float FireHeightOffset => fireHeightOffset;
        public float FireFadeOutDuration => fireFadeOutDuration;
    }
}
