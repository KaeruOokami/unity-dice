using UnityEngine;

namespace DiceGame.Config
{
    /// <summary>
    /// FaceWeight（2〜6）からプレイヤー Emission 色を解決する。
    /// 最頻値を基準とし、同率なら小さい方を基準にする。
    /// </summary>
    public static class AttackFaceEmissionColorResolver
    {
        const int FaceCount = 5;

        public static Color Resolve(
            Color neutralColor,
            Color face2Color,
            Color face3Color,
            Color face4Color,
            Color face5Color,
            Color face6Color,
            float face2Weight,
            float face3Weight,
            float face4Weight,
            float face5Weight,
            float face6Weight) {
            var weights = new float[FaceCount] {
                Mathf.Max(0f, face2Weight),
                Mathf.Max(0f, face3Weight),
                Mathf.Max(0f, face4Weight),
                Mathf.Max(0f, face5Weight),
                Mathf.Max(0f, face6Weight)
            };
            var colors = new Color[FaceCount] {
                face2Color,
                face3Color,
                face4Color,
                face5Color,
                face6Color
            };

            var baseline = FindModePreferSmaller(weights);
            var hasAbove = false;
            var hasBelow = false;
            for (var i = 0; i < FaceCount; i++) {
                if (weights[i] > baseline) {
                    hasAbove = true;
                } else if (weights[i] < baseline) {
                    hasBelow = true;
                }
            }

            if (!hasAbove && !hasBelow) {
                return WithOpaqueAlpha(neutralColor);
            }

            if (hasAbove && !hasBelow) {
                return MixAboveByAbsoluteWeights(colors, weights, baseline);
            }

            if (!hasAbove && hasBelow) {
                return EqualMixAtBaseline(colors, weights, baseline);
            }

            return MixSignedAbsoluteWeights(colors, weights, baseline);
        }

        static float FindModePreferSmaller(float[] weights) {
            var bestCount = 0;
            var bestValue = weights[0];

            for (var i = 0; i < weights.Length; i++) {
                var candidate = weights[i];
                var count = 0;
                for (var j = 0; j < weights.Length; j++) {
                    if (weights[j] == candidate) {
                        count++;
                    }
                }

                if (count > bestCount || (count == bestCount && candidate < bestValue)) {
                    bestCount = count;
                    bestValue = candidate;
                }
            }

            return bestValue;
        }

        static Color MixAboveByAbsoluteWeights(Color[] colors, float[] weights, float baseline) {
            var rgb = Vector3.zero;
            var sum = 0f;
            for (var i = 0; i < FaceCount; i++) {
                if (weights[i] <= baseline) {
                    continue;
                }

                var w = weights[i];
                if (w <= 0f) {
                    continue;
                }

                rgb += ToRgb(colors[i]) * w;
                sum += w;
            }

            if (sum <= 0f) {
                return Color.white;
            }

            return FromRgb(rgb / sum);
        }

        static Color EqualMixAtBaseline(Color[] colors, float[] weights, float baseline) {
            var rgb = Vector3.zero;
            var count = 0;
            for (var i = 0; i < FaceCount; i++) {
                if (weights[i] != baseline) {
                    continue;
                }

                rgb += ToRgb(colors[i]);
                count++;
            }

            if (count == 0) {
                return Color.white;
            }

            return FromRgb(rgb / count);
        }

        static Color MixSignedAbsoluteWeights(Color[] colors, float[] weights, float baseline) {
            var rgb = Vector3.zero;
            var sum = 0f;
            for (var i = 0; i < FaceCount; i++) {
                var w = weights[i];
                if (w == baseline || w <= 0f) {
                    continue;
                }

                var signed = w > baseline ? w : -w;
                rgb += ToRgb(colors[i]) * signed;
                sum += w;
            }

            if (sum <= 0f) {
                return Color.white;
            }

            rgb /= sum;
            return FromRgb(new Vector3(
                Mathf.Clamp01(rgb.x),
                Mathf.Clamp01(rgb.y),
                Mathf.Clamp01(rgb.z)));
        }

        static Vector3 ToRgb(Color color) {
            return new Vector3(color.r, color.g, color.b);
        }

        static Color FromRgb(Vector3 rgb) {
            return new Color(rgb.x, rgb.y, rgb.z, 1f);
        }

        static Color WithOpaqueAlpha(Color color) {
            return new Color(color.r, color.g, color.b, 1f);
        }
    }
}
