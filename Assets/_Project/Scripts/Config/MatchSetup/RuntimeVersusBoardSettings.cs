using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Config
{
    public sealed class RuntimeVersusBoardSettings : IVersusBoardSettings
    {
        readonly VersusBoardSettings template;
        readonly PlayerBoardDefinition player1;
        readonly PlayerBoardDefinition player2;

        public RuntimeVersusBoardSettings(
            VersusBoardSettings template,
            PlayerSlotSetup player1Setup,
            PlayerSlotSetup player2Setup) {
            this.template = template;
            var sharedInitialDiceCount = Mathf.Max(1, player1Setup.Spawn.InitialDiceCount);
            var player1Spawn = player1Setup.Spawn.WithInitialDiceCount(sharedInitialDiceCount);
            var player2Spawn = player2Setup.Spawn.WithInitialDiceCount(sharedInitialDiceCount);
            player1 = new PlayerBoardDefinition(
                template.Player1.Width,
                template.Player1.Height,
                player1Spawn.ToRuntimeAsset(),
                player1Setup.Catalog.ToRuntimeAsset(),
                player1Setup.Attack.ToRuntimeAsset(),
                player1Setup.NaturalSend.ToRuntimeAsset());
            player2 = new PlayerBoardDefinition(
                template.Player2.Width,
                template.Player2.Height,
                player2Spawn.ToRuntimeAsset(),
                player2Setup.Catalog.ToRuntimeAsset(),
                player2Setup.Attack.ToRuntimeAsset(),
                player2Setup.NaturalSend.ToRuntimeAsset());
        }

        public PlayerBoardDefinition Player1 => player1;
        public PlayerBoardDefinition Player2 => player2;
        public VersusInitialDicePlacementMode InitialDicePlacementMode =>
            template.InitialDicePlacementMode;
        public AttackQueueUiSettings AttackQueueUiSettings => template.AttackQueueUiSettings;
        public JumboDiceSettings JumboDiceSettings => template.JumboDiceSettings;

        public VersusArenaLayout CreateLayout() {
            return template.CreateLayout();
        }

        public DiceSpawnSettings GetSpawnSettings(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1.SpawnSettings : player2.SpawnSettings;
        }

        public PlayerAttackSettings GetAttackSettings(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1.AttackSettings : player2.AttackSettings;
        }

        public DiceCatalog GetDiceCatalog(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1.DiceCatalog : player2.DiceCatalog;
        }

        public PlayerNaturalSendSettings GetNaturalSendSettings(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1.NaturalSendSettings : player2.NaturalSendSettings;
        }

        public bool TryValidate(out string errorMessage) {
            if (player1.SpawnSettings == null || player2.SpawnSettings == null) {
                errorMessage = "RuntimeVersusBoardSettings: Each player requires DiceSpawnSettings.";
                return false;
            }

            if (player1.DiceCatalog == null || player2.DiceCatalog == null) {
                errorMessage = "RuntimeVersusBoardSettings: Each player requires DiceCatalog.";
                return false;
            }

            if (player1.SpawnSettings.InitialDiceCount != player2.SpawnSettings.InitialDiceCount) {
                errorMessage =
                    "RuntimeVersusBoardSettings: InitialDiceCount must be shared between Player1 and Player2.";
                return false;
            }

            if (InitialDicePlacementMode == VersusInitialDicePlacementMode.Mirrored) {
                if (player1.Width != player2.Width || player1.Height != player2.Height) {
                    errorMessage =
                        "RuntimeVersusBoardSettings: Mirrored initial dice placement requires matching board sizes.";
                    return false;
                }
            }

            if (player1.AttackSettings == null || player2.AttackSettings == null) {
                errorMessage = "RuntimeVersusBoardSettings: Each player requires PlayerAttackSettings.";
                return false;
            }

            if (!player1.AttackSettings.TryValidate(out errorMessage)) {
                return false;
            }

            if (!player2.AttackSettings.TryValidate(out errorMessage)) {
                return false;
            }

            if (player1.NaturalSendSettings != null
                && !player1.NaturalSendSettings.TryValidate(out errorMessage)) {
                return false;
            }

            if (player2.NaturalSendSettings != null
                && !player2.NaturalSendSettings.TryValidate(out errorMessage)) {
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
