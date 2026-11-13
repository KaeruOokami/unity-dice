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
            return Calculate(settings, face, face, chainCount, clusterSize, isSnatch);
        }

        /// <param name="powerFace">Face used for FaceGain / face weight (cleared visible face).</param>
        /// <param name="matchFace">Cleared match face used for size bonus (always 2–6).</param>
        public static float Calculate(
            PlayerAttackSettings settings,
            int powerFace,
            int matchFace,
            int chainCount,
            int clusterSize,
            bool isSnatch) {
            if (settings == null
                || powerFace < MinMatchFace
                || powerFace > MaxMatchFace
                || matchFace < MinMatchFace
                || matchFace > MaxMatchFace) {
                return 0f;
            }

            var faceFactor = 1f + (powerFace - MinMatchFace) * settings.FaceGain;
            var faceWeight = settings.GetFaceWeight(powerFace);
            var chainFactor = 1f + chainCount * settings.ChainGain;
            var extraDice = Mathf.Max(0, clusterSize - matchFace);
            var sizeFactor = 1f + extraDice * settings.SizeGain;
            var raw = faceFactor * faceWeight * chainFactor * sizeFactor * settings.AttackMultiplier;

            if (isSnatch) {
                raw *= settings.SnatchMultiplier;
            }

            return Mathf.Clamp01(raw);
        }
    }
}
