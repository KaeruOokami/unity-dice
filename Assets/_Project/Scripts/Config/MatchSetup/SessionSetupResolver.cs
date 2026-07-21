using DiceGame.Session;

namespace DiceGame.Config
{
    public sealed class ResolvedSessionSetup
    {
        public GameMode GameMode { get; private set; }
        public int RequiredPlayerCount { get; private set; }
        public DiceSpawnSettings SharedSpawnSettings { get; private set; }
        public DiceCatalog SharedDiceCatalog { get; private set; }
        public IVersusBoardSettings VersusBoardSettings { get; private set; }
        public bool Player1IsAi { get; private set; }
        public bool Player2IsAi { get; private set; }
        public PlayerSlotInputConfig Player1Input { get; private set; }
        public PlayerSlotInputConfig Player2Input { get; private set; }

        public bool IsAiControlled(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? Player1IsAi : Player2IsAi;
        }

        public PlayerSlotInputConfig GetInputConfig(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? Player1Input : Player2Input;
        }

        public static ResolvedSessionSetup Resolve(
            GameSessionSettings gameSessionSettings,
            DiceSpawnSettings sceneSpawnSettings,
            DiceCatalog sceneDiceCatalog,
            MatchSetupPresetRegistry presetRegistry,
            PlayerInputSettings playerInputSettings,
            MatchSetupSnapshot runtimeSetup) {
            if (runtimeSetup != null) {
                return FromSnapshot(gameSessionSettings, runtimeSetup);
            }

            return FromAssets(
                gameSessionSettings,
                sceneSpawnSettings,
                sceneDiceCatalog,
                presetRegistry,
                playerInputSettings);
        }

        static ResolvedSessionSetup FromSnapshot(
            GameSessionSettings gameSessionSettings,
            MatchSetupSnapshot snapshot) {
            IVersusBoardSettings versusSettings = null;
            if (snapshot.GameMode == GameMode.Versus) {
                versusSettings = new RuntimeVersusBoardSettings(
                    gameSessionSettings.VersusBoardSettings,
                    snapshot.Player1,
                    snapshot.Player2);
            }

            return new ResolvedSessionSetup {
                GameMode = snapshot.GameMode,
                RequiredPlayerCount = snapshot.RequiredPlayerCount,
                SharedSpawnSettings = snapshot.SharedSpawn.ToRuntimeAsset(),
                SharedDiceCatalog = snapshot.SharedCatalog.ToRuntimeAsset(),
                VersusBoardSettings = versusSettings,
                Player1IsAi = snapshot.Player1.IsAi,
                Player2IsAi = snapshot.Player2.IsAi,
                Player1Input = snapshot.Player1.InputConfig,
                Player2Input = snapshot.Player2.InputConfig
            };
        }

        static ResolvedSessionSetup FromAssets(
            GameSessionSettings gameSessionSettings,
            DiceSpawnSettings sceneSpawnSettings,
            DiceCatalog sceneDiceCatalog,
            MatchSetupPresetRegistry presetRegistry,
            PlayerInputSettings playerInputSettings) {
            var session = OnlineSessionState.Instance;
            var isOnline = session != null && session.IsOnline;
            var player1Control = ResolveControlDefaults(presetRegistry, playerInputSettings, PlayerSlot.Player1);
            var player2Control = ResolveControlDefaults(presetRegistry, playerInputSettings, PlayerSlot.Player2);

            return new ResolvedSessionSetup {
                GameMode = gameSessionSettings.GameMode,
                RequiredPlayerCount = gameSessionSettings.RequiredPlayerCount,
                SharedSpawnSettings = sceneSpawnSettings,
                SharedDiceCatalog = sceneDiceCatalog,
                VersusBoardSettings = gameSessionSettings.VersusBoardSettings,
                Player1IsAi = !isOnline && player1Control.IsAi,
                Player2IsAi = !isOnline && player2Control.IsAi,
                Player1Input = player1Control.InputConfig,
                Player2Input = player2Control.InputConfig
            };
        }

        static PlayerSlotControlDefaults ResolveControlDefaults(
            MatchSetupPresetRegistry presetRegistry,
            PlayerInputSettings playerInputSettings,
            PlayerSlot slot) {
            if (presetRegistry != null && presetRegistry.DefaultPlayerInputSettings != null) {
                return presetRegistry.GetControlDefaults(slot);
            }

            if (playerInputSettings != null) {
                return playerInputSettings.GetControlDefaults(slot);
            }

            return slot == PlayerSlot.Player1
                ? PlayerSlotControlDefaults.Create(false, PlayerInputDeviceKind.Keyboard, 0)
                : PlayerSlotControlDefaults.Create(true, PlayerInputDeviceKind.Gamepad, 0);
        }
    }
}
