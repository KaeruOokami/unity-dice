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
                out volley,
                out _);
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
            return TryBuild(
                settings,
                face,
                chainCount,
                clusterSize,
                isSnatch,
                random,
                jumboSendableRemaining,
                out volley,
                out _);
        }

        public static bool TryBuild(
            PlayerAttackSettings settings,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch,
            System.Random random,
            int jumboSendableRemaining,
            out AttackVolley volley,
            out float attackPower) {
            volley = null;
            attackPower = 0f;
            if (settings == null || random == null) {
                return false;
            }

            if (!settings.TryGetSendableKindsForFace(face, out var sendableKinds)) {
                return false;
            }

            attackPower = AttackPowerCalculator.Calculate(
                settings,
                face,
                chainCount,
                clusterSize,
                isSnatch);
            var diceCount = AttackVolumeResolver.ResolveDiceCount(sendableKinds, attackPower);
            if (diceCount <= 0) {
                return false;
            }

            if (!AttackKindBreakdown.TryBuild(
                    sendableKinds,
                    diceCount,
                    attackPower,
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
