using System;

namespace DiceGame.Config
{
    [Serializable]
    public struct PlayerSlotSetup
    {
        public bool IsAi;
        public PlayerSlotInputConfig InputConfig;
        public DiceSpawnSettingsData Spawn;
        public DiceCatalogData Catalog;
        public PlayerAttackSettingsData Attack;
        public PlayerNaturalSendSettingsData NaturalSend;

        public static PlayerSlotSetup CreateDefault(
            bool isAi,
            PlayerSlotInputConfig inputConfig,
            DiceSpawnSettingsData spawn = default,
            DiceCatalogData catalog = default,
            PlayerAttackSettingsData attack = default,
            PlayerNaturalSendSettingsData naturalSend = default) {
            return new PlayerSlotSetup {
                IsAi = isAi,
                InputConfig = inputConfig,
                Spawn = spawn,
                Catalog = catalog.Entries != null ? catalog : DiceCatalogData.Empty(),
                Attack = attack.FaceSendProfiles != null ? attack : PlayerAttackSettingsData.Default(),
                NaturalSend = naturalSend.SendableKinds != null
                    ? naturalSend
                    : PlayerNaturalSendSettingsData.Empty()
            };
        }
    }
}
