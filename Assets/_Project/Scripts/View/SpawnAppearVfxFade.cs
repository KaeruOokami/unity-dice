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
        int stripSizeMinId;
        float fadeDuration;

        public void Begin(
            VisualEffect effect,
            int stripSizeMaxPropertyId,
            int stripSizeMinPropertyId,
            float duration) {
            visualEffect = effect;
            stripSizeMaxId = stripSizeMaxPropertyId;
            stripSizeMinId = stripSizeMinPropertyId;
            fadeDuration = Mathf.Max(0.01f, duration);
            StartCoroutine(FadeOutCoroutine());
        }

        IEnumerator FadeOutCoroutine() {
            if (visualEffect == null) {
                Destroy(gameObject);
                yield break;
            }

            var fromMax = visualEffect.HasFloat(stripSizeMaxId)
                ? visualEffect.GetFloat(stripSizeMaxId)
                : 0f;
            var fromMin = visualEffect.HasFloat(stripSizeMinId)
                ? visualEffect.GetFloat(stripSizeMinId)
                : 0f;
            var elapsed = 0f;
            while (elapsed < fadeDuration && visualEffect != null) {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / fadeDuration);
                visualEffect.SetFloat(stripSizeMaxId, Mathf.Lerp(fromMax, 0f, t));
                visualEffect.SetFloat(stripSizeMinId, Mathf.Lerp(fromMin, 0f, t));
                yield return null;
            }

            if (visualEffect != null) {
                visualEffect.SetFloat(stripSizeMaxId, 0f);
                visualEffect.SetFloat(stripSizeMinId, 0f);
            }

            Destroy(gameObject);
        }
    }
}
