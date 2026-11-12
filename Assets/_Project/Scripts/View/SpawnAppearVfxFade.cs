using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace DiceGame.View
{
    /// <summary>
    /// Owns fade-out + destroy for spawn-appear Lightning so cleanup does not depend on
    /// the spawning <see cref="DiceView"/> remaining alive.
    /// </summary>
    public sealed class SpawnAppearVfxFade : MonoBehaviour
    {
        VisualEffect visualEffect;
        int stripSizeMaxId;
        float fadeDuration;

        public void Begin(VisualEffect effect, int stripSizeMaxPropertyId, float duration) {
            visualEffect = effect;
            stripSizeMaxId = stripSizeMaxPropertyId;
            fadeDuration = Mathf.Max(0.01f, duration);
            StartCoroutine(FadeOutCoroutine());
        }

        IEnumerator FadeOutCoroutine() {
            if (visualEffect == null) {
                Destroy(gameObject);
                yield break;
            }

            var fromSize = visualEffect.HasFloat(stripSizeMaxId)
                ? visualEffect.GetFloat(stripSizeMaxId)
                : 0f;
            var elapsed = 0f;
            while (elapsed < fadeDuration && visualEffect != null) {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / fadeDuration);
                visualEffect.SetFloat(stripSizeMaxId, Mathf.Lerp(fromSize, 0f, t));
                yield return null;
            }

            if (visualEffect != null) {
                visualEffect.SetFloat(stripSizeMaxId, 0f);
            }

            Destroy(gameObject);
        }
    }
}
