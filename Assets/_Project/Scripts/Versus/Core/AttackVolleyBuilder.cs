using System.Collections.Generic;
using DiceGame.Config;

namespace DiceGame.Versus.Core
{
    public static class AttackVolleyBuilder
    {
        public static bool TryBuild(
            PlayerAttackSettings settings,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch,
            System.Random random,
            out AttackVolley volley) {
            volley = null;
            if (settings == null || random == null) {
                return false;
            }

            var power = AttackPowerCalculator.Calculate(settings, face, chainCount, clusterSize, isSnatch);
            var diceCount = AttackVolumeResolver.ResolveDiceCount(settings, power);
            if (diceCount <= 0) {
                return false;
            }

            if (!AttackKindBreakdown.TryBuild(settings, diceCount, random, out var breakdown)) {
                return false;
            }

            var specs = AttackFaceRoller.RollDice(breakdown, face, random);
            if (specs.Count == 0) {
                return false;
            }

            volley = new AttackVolley(specs);
            return true;
        }
    }
}
