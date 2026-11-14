using DiceGame.Config;

namespace DiceGame.Session
{
    enum AttackPresetKind
    {
        Default,
        User
    }

    readonly struct AttackPresetListEntry
    {
        public AttackPresetListEntry(
            AttackPresetKind kind,
            string name,
            PlayerAttackSettings defaultSource = null,
            PlayerAttackPresentationSettings defaultPresentation = null) {
            Kind = kind;
            Name = name;
            DefaultSource = defaultSource;
            DefaultPresentation = defaultPresentation;
        }

        public AttackPresetKind Kind { get; }
        public string Name { get; }
        public PlayerAttackSettings DefaultSource { get; }
        public PlayerAttackPresentationSettings DefaultPresentation { get; }

        public string DropdownLabel =>
            Kind == AttackPresetKind.Default
                ? AttackPresetLabels.DefaultPrefix + Name
                : AttackPresetLabels.UserPrefix + Name;
    }
}
