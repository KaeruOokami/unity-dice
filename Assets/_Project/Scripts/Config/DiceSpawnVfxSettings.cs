using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceSpawnVfxSettings", menuName = "Dice/Dice Spawn Vfx Settings")]
    public class DiceSpawnVfxSettings : ScriptableObject
    {
        [SerializeField] VisualEffect prefab;
        [SerializeField] float startHeight = 20f;
        [FormerlySerializedAs("destroyDelay")]
        [SerializeField] float fadeOutDuration = 1f;

        public VisualEffect Prefab => prefab;
        public float StartHeight => startHeight;
        public float FadeOutDuration => fadeOutDuration;
    }
}
