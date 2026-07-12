using System.Collections.Generic;
using DiceGame.Core;

namespace DiceGame.Versus.Core
{
    public static class AttackFaceRoller
    {
        public static List<AttackDieSpec> RollDice(
            IReadOnlyList<(DiceKind kind, int count)> breakdown,
            int faceCap,
            System.Random random) {
            var results = new List<AttackDieSpec>();
            if (breakdown == null || random == null || faceCap < 1) {
                return results;
            }

            var cap = UnityEngine.Mathf.Clamp(faceCap, 1, 6);
            for (var i = 0; i < breakdown.Count; i++) {
                var entry = breakdown[i];
                for (var j = 0; j < entry.count; j++) {
                    var pip = random.Next(1, cap + 1);
                    results.Add(new AttackDieSpec(entry.kind, pip));
                }
            }

            Shuffle(results, random);
            return results;
        }

        static void Shuffle(List<AttackDieSpec> specs, System.Random random) {
            for (var i = specs.Count - 1; i > 0; i--) {
                var j = random.Next(i + 1);
                (specs[i], specs[j]) = (specs[j], specs[i]);
            }
        }
    }
}
