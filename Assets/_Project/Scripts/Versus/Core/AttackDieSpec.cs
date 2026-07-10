using DiceGame.Core;

namespace DiceGame.Versus.Core
{
    public readonly struct AttackDieSpec
    {
        public DiceKind Kind { get; }
        public int Pip { get; }

        public AttackDieSpec(DiceKind kind, int pip) {
            Kind = kind;
            Pip = pip;
        }
    }
}
