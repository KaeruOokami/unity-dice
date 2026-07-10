using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Versus.Core
{
    public static class AttackPowerCalculator
    {
        public static float Calculate(
            PlayerAttackSettings settings,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch) {
            if (settings == null || face < 2 || face > 6) {
                return 0f;
            }

            var faceFactor = face / 6f;
            var chainFactor = 1f + chainCount * settings.ChainGain;
            var extraDice = Mathf.Max(0, clusterSize - face);
            var sizeFactor = 1f + extraDice * settings.SizeGain;
            var raw = faceFactor * chainFactor * sizeFactor * settings.AttackMultiplier;

            if (isSnatch) {
                raw *= settings.SnatchMultiplier;
            }

            return Mathf.Clamp01(raw);
        }
    }
}
