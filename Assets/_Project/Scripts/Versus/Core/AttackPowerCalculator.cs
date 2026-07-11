using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Versus.Core
{
    public static class AttackPowerCalculator
    {
        const int MinMatchFace = 2;
        const int MaxMatchFace = 6;

        public static float Calculate(
            PlayerAttackSettings settings,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch) {
            if (settings == null || face < MinMatchFace || face > MaxMatchFace) {
                return 0f;
            }

            var faceFactor = 1f + (face - MinMatchFace) * settings.FaceGain;
            var faceWeight = settings.GetFaceWeight(face);
            var chainFactor = 1f + chainCount * settings.ChainGain;
            var extraDice = Mathf.Max(0, clusterSize - face);
            var sizeFactor = 1f + extraDice * settings.SizeGain;
            var raw = faceFactor * faceWeight * chainFactor * sizeFactor * settings.AttackMultiplier;

            if (isSnatch) {
                raw *= settings.SnatchMultiplier;
            }

            return Mathf.Clamp01(raw);
        }
    }
}
