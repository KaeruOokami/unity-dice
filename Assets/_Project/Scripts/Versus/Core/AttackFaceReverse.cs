using System.Collections.Generic;
using DiceGame.Core;

namespace DiceGame.Versus.Core
{
    /// <summary>
    /// Maps match/erasure faces to reverse-mode attack effect faces and pips.
    /// Effect face is OppositeFace(matchFace); rolled pips are flipped the same way.
    /// </summary>
    public static class AttackFaceReverse
    {
        public static int ToEffectFace(int matchFace) {
            return DiceOrientation.OppositeFace(matchFace);
        }

        public static void FlipPipsInPlace(List<AttackDieSpec> specs) {
            if (specs == null) {
                return;
            }

            for (var i = 0; i < specs.Count; i++) {
                var spec = specs[i];
                specs[i] = new AttackDieSpec(
                    spec.Kind,
                    DiceOrientation.OppositeFace(spec.Pip),
                    spec.IsOnFire);
            }
        }
    }
}
