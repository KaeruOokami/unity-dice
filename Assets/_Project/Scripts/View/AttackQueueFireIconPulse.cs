using DiceGame.Config;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.View
{
    /// <summary>
    /// Pulses an attack-queue icon between idle and red glow while the die is on fire.
    /// Phase is driven by unscaled time so all fire icons blink in sync.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class AttackQueueFireIconPulse : MonoBehaviour
    {
        Image image;
        float periodSeconds = 0.85f;
        Color idleColor = Color.white;
        Color glowColor = new(1f, 0.28f, 0.08f, 1f);

        public void Configure(AttackQueueSettings settings) {
            if (settings == null) {
                enabled = false;
                return;
            }

            periodSeconds = settings.FireIconPulsePeriodSeconds;
            idleColor = settings.FireIconIdleColor;
            glowColor = settings.FireIconGlowColor;
            EnsureImage();
            enabled = image != null;
        }

        void Awake() {
            EnsureImage();
        }

        void EnsureImage() {
            if (image == null) {
                image = GetComponent<Image>();
            }
        }

        void LateUpdate() {
            if (image == null || periodSeconds <= 0f) {
                return;
            }

            var phase = (Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / periodSeconds)) + 1f) * 0.5f;
            image.color = Color.Lerp(idleColor, glowColor, phase);
        }

        void OnDisable() {
            if (image != null) {
                image.color = idleColor;
            }
        }
    }
}
