using System.Collections.Generic;

namespace DiceGame.Versus.Core
{
    public sealed class AttackVolley
    {
        readonly List<AttackDieSpec> dice;

        public AttackVolley(List<AttackDieSpec> diceSpecs) {
            dice = diceSpecs ?? new List<AttackDieSpec>();
        }

        public IReadOnlyList<AttackDieSpec> Dice => dice;
        public int Count => dice.Count;
    }
}
