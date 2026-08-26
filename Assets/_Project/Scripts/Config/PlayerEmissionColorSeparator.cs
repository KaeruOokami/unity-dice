using UnityEngine;

namespace DiceGame.Config
{
    /// <summary>
    /// Versus で 1P/2P Emission が近いとき、スロットデフォルト色へ段階的に寄せて分離する。
    /// </summary>
    public static class PlayerEmissionColorSeparator
    {
        static readonly float[] DefaultBlendSteps = { 0.35f, 0.55f, 0.75f, 1f };

        public static void SeparateTowardDefaults(
            Color color1,
            Color color2,
            Color default1,
            Color default2,
            float minHueSeparation,
            float lowChromaThreshold,
            float[] blendSteps,
            out Color separated1,
            out Color separated2) {
            var source1 = WithOpaqueAlpha(color1);
            var source2 = WithOpaqueAlpha(color2);
            separated1 = source1;
            separated2 = source2;

            if (!AreTooSimilar(source1, source2, minHueSeparation, lowChromaThreshold)) {
                return;
            }

            var steps = blendSteps != null && blendSteps.Length > 0 ? blendSteps : DefaultBlendSteps;
            for (var i = 0; i < steps.Length; i++) {
                var t = Mathf.Clamp01(steps[i]);
                separated1 = WithOpaqueAlpha(Color.Lerp(source1, default1, t));
                separated2 = WithOpaqueAlpha(Color.Lerp(source2, default2, t));
                if (!AreTooSimilar(separated1, separated2, minHueSeparation, lowChromaThreshold)) {
                    return;
                }
            }
        }

        public static bool AreTooSimilar(
            Color a,
            Color b,
            float minHueSeparation,
            float lowChromaThreshold) {
            Color.RGBToHSV(a, out var h1, out var s1, out _);
            Color.RGBToHSV(b, out var h2, out var s2, out _);

            var achromatic1 = s1 < lowChromaThreshold;
            var achromatic2 = s2 < lowChromaThreshold;
            if (achromatic1 && achromatic2) {
                return true;
            }

            if (achromatic1 || achromatic2) {
                return false;
            }

            var dh = Mathf.Abs(h1 - h2);
            if (dh > 0.5f) {
                dh = 1f - dh;
            }

            return dh < minHueSeparation;
        }

        static Color WithOpaqueAlpha(Color color) {
            return new Color(color.r, color.g, color.b, 1f);
        }
    }
}
