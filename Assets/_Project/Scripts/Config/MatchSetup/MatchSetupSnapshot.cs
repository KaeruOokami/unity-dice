namespace DiceGame.Config
{
    public sealed class MatchSetupSnapshot
    {
        public GameMode GameMode { get; set; }
        public DiceSpawnSettingsData SharedSpawn { get; set; }
        public DiceCatalogData SharedCatalog { get; set; }
        public PlayerSlotSetup Player1 { get; set; }
        public PlayerSlotSetup Player2 { get; set; }

        public int RequiredPlayerCount => GameMode == GameMode.Single ? 1 : 2;

        public bool TryValidate(MatchSetupPresetRegistry registry, out string errorMessage) {
            if (registry == null) {
                errorMessage = "MatchSetupSnapshot: Preset registry is not assigned.";
                return false;
            }

            if (GameMode == GameMode.Versus) {
                return TryValidateVersus(out errorMessage);
            }

            if (!SharedSpawn.TryValidate(out errorMessage)) {
                return false;
            }

            if (!SharedCatalog.TryValidate(out errorMessage)) {
                return false;
            }

            if (!TryValidatePlayerSlot(PlayerSlot.Player1, requireVersusAssets: false, out errorMessage)) {
                return false;
            }

            if (RequiredPlayerCount >= 2
                && !TryValidatePlayerSlot(PlayerSlot.Player2, requireVersusAssets: false, out errorMessage)) {
                return false;
            }

            errorMessage = null;
            return true;
        }

        bool TryValidateVersus(out string errorMessage) {
            if (!TryValidatePlayerSlot(PlayerSlot.Player1, requireVersusAssets: true, out errorMessage)) {
                return false;
            }

            if (!TryValidatePlayerSlot(PlayerSlot.Player2, requireVersusAssets: true, out errorMessage)) {
                return false;
            }

            errorMessage = null;
            return true;
        }

        bool TryValidatePlayerSlot(PlayerSlot slot, bool requireVersusAssets, out string errorMessage) {
            var setup = slot == PlayerSlot.Player1 ? Player1 : Player2;

            if (requireVersusAssets) {
                if (!setup.Spawn.TryValidate(out errorMessage)) {
                    return false;
                }

                if (!setup.Catalog.TryValidate(out errorMessage)) {
                    return false;
                }

                if (!setup.Attack.TryValidate(out errorMessage)) {
                    return false;
                }

                if (!setup.NaturalSend.TryValidate(out errorMessage)) {
                    return false;
                }
            }

            if (setup.IsAi) {
                errorMessage = null;
                return true;
            }

            if (setup.InputConfig.DeviceKind == PlayerInputDeviceKind.Gamepad
                && setup.InputConfig.GamepadIndex < 0) {
                errorMessage = $"MatchSetupSnapshot: {slot} gamepad index is invalid.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public PlayerSlotSetup GetPlayerSetup(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? Player1 : Player2;
        }

        public MatchSetupSnapshot Clone() {
            return new MatchSetupSnapshot {
                GameMode = GameMode,
                SharedSpawn = SharedSpawn,
                SharedCatalog = SharedCatalog,
                Player1 = Player1,
                Player2 = Player2
            };
        }
    }
}
