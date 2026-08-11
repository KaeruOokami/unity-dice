using System;

namespace DiceGame.Session
{
    [Serializable]
    public sealed class AttackPresetPersistFile
    {
        public int Version;
        public string Name;
        public PlayerAttackSettingsPersistDto Attack = new();
    }
}
