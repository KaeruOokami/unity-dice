using System.Collections.Generic;
using DiceGame.Config;

namespace DiceGame.Versus.Core
{
    public static class NaturalSendVolleyBuilder
    {
        const int NaturalSendFaceCap = 6;

        public static bool TryBuild(
            PlayerNaturalSendSettings settings,
            System.Random random,
            out AttackVolley volley) {
            return TryBuild(settings, random, int.MaxValue, out volley);
        }

        public static bool TryBuild(
            PlayerNaturalSendSettings settings,
            System.Random random,
            int jumboSendableRemaining,
            out AttackVolley volley) {
            volley = null;
            if (settings == null || random == null || !settings.Enabled) {
                return false;
            }

            var diceCount = settings.DiceCountPerVolley;
            if (!NaturalSendKindBreakdown.TryBuild(
                    settings,
                    diceCount,
                    random,
                    jumboSendableRemaining,
                    out var breakdown)) {
                return false;
            }

            var specs = AttackFaceRoller.RollDice(breakdown, NaturalSendFaceCap, random);
            if (specs.Count == 0) {
                return false;
            }

            volley = new AttackVolley(specs);
            return true;
        }
    }
}
