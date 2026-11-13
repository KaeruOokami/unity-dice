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
                reverseMode: false,
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
                reverseMode: false,
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
            return TryBuild(
                settings,
                face,
                chainCount,
                clusterSize,
                isSnatch,
                random,
                jumboSendableRemaining,
                reverseMode: false,
                out volley,
                out attackPower);
        }

        public static bool TryBuild(
            PlayerAttackSettings settings,
            int face,
            int chainCount,
            int clusterSize,
            bool isSnatch,
            System.Random random,
            int jumboSendableRemaining,
            bool reverseMode,
            out AttackVolley volley,
            out float attackPower) {
            volley = null;
            attackPower = 0f;
            if (settings == null || random == null) {
                return false;
            }

            var matchFace = face;
            var effectFace = reverseMode ? AttackFaceReverse.ToEffectFace(matchFace) : matchFace;

            if (!settings.TryGetSendableKindsForFace(effectFace, out var sendableKinds)) {
                return false;
            }

            // FaceGain / face weight stay on the cleared (visible) face; profile + pip range use effectFace.
            attackPower = AttackPowerCalculator.Calculate(
                settings,
                matchFace,
                matchFace,
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

            var specs = AttackFaceRoller.RollDice(breakdown, effectFace, random);
            if (specs.Count == 0) {
                return false;
            }

            if (reverseMode) {
                AttackFaceReverse.FlipPipsInPlace(specs);
            }

            volley = new AttackVolley(specs);
            return true;
        }
    }
}
