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
            return TryBuild(
                settings,
                face,
                chainCount,
                clusterSize,
                isSnatch,
                random,
                int.MaxValue,
                out volley);
        }

        public static bool TryBuild(
            PlayerAttackSettings settings,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch,
            System.Random random,
            int jumboSendableRemaining,
            out AttackVolley volley) {
            volley = null;
            if (settings == null || random == null) {
                return false;
            }

            if (!settings.TryGetSendableKindsForFace(face, out var sendableKinds)) {
                return false;
            }

            var power = AttackPowerCalculator.Calculate(settings, face, chainCount, clusterSize, isSnatch);
            var diceCount = AttackVolumeResolver.ResolveDiceCount(sendableKinds, power);
            if (diceCount <= 0) {
                return false;
            }

            if (!AttackKindBreakdown.TryBuild(
                    sendableKinds,
                    diceCount,
                    power,
                    random,
                    jumboSendableRemaining,
                    out var breakdown)) {
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
