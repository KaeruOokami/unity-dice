using DiceGame.Core;

namespace DiceGame.Versus.Core
{
    public readonly struct AttackDieSpec
    {
        public DiceKind Kind { get; }
        public int Pip { get; }
        public bool IsOnFire { get; }

        public AttackDieSpec(DiceKind kind, int pip, bool isOnFire = false) {
            Kind = kind;
            Pip = pip;
            IsOnFire = isOnFire;
        }
    }
}
