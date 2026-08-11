using System;

namespace DiceGame.Session
{
    [Serializable]
    public sealed class PlayerBindingOverridesPersistFile
    {
        public int Version;
        public string OverridesJson;
    }
}
