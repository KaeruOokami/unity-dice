using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "MatchSetupPresetRegistry", menuName = "Dice/Match Setup Preset Registry")]
    public sealed class MatchSetupPresetRegistry : ScriptableObject
    {
        [SerializeField] DiceSpawnSettings[] spawnPresets = System.Array.Empty<DiceSpawnSettings>();
        [SerializeField] DiceCatalog[] catalogPresets = System.Array.Empty<DiceCatalog>();
        [SerializeField] PlayerAttackSettings[] attackPresets = System.Array.Empty<PlayerAttackSettings>();
        [SerializeField] PlayerNaturalSendSettings[] naturalSendPresets =
            System.Array.Empty<PlayerNaturalSendSettings>();

        [Header("Local Defaults")]
        [SerializeField] PlayerInputSettings defaultPlayerInputSettings;
        [SerializeField] DiceSpawnSettings defaultSharedSpawn;
        [SerializeField] DiceCatalog defaultSharedCatalog;
        [SerializeField] DiceSpawnSettings defaultPlayer1Spawn;
        [SerializeField] DiceCatalog defaultPlayer1Catalog;
        [SerializeField] PlayerAttackSettings defaultPlayer1Attack;
        [SerializeField] PlayerNaturalSendSettings defaultPlayer1NaturalSend;
        [SerializeField] DiceSpawnSettings defaultPlayer2Spawn;
        [SerializeField] DiceCatalog defaultPlayer2Catalog;
        [SerializeField] PlayerAttackSettings defaultPlayer2Attack;
        [SerializeField] PlayerNaturalSendSettings defaultPlayer2NaturalSend;

        public DiceSpawnSettings[] SpawnPresets => spawnPresets;
        public DiceCatalog[] CatalogPresets => catalogPresets;
        public PlayerAttackSettings[] AttackPresets => attackPresets;
        public PlayerNaturalSendSettings[] NaturalSendPresets => naturalSendPresets;
        public PlayerInputSettings DefaultPlayerInputSettings => defaultPlayerInputSettings;

        public MatchSetupSnapshot CreateDefaultSnapshot(GameMode mode) {
            if (defaultPlayerInputSettings == null) {
                Debug.LogError("[MatchSetupPresetRegistry] defaultPlayerInputSettings is not assigned.");
            }

            return new MatchSetupSnapshot {
                GameMode = mode,
                SharedSpawn = DiceSpawnSettingsData.FromTemplate(defaultSharedSpawn),
                SharedCatalog = DiceCatalogData.FromTemplate(defaultSharedCatalog),
                Player1 = CreateDefaultPlayerSetup(
                    GetControlDefaults(PlayerSlot.Player1),
                    defaultPlayer1Spawn,
                    defaultPlayer1Catalog,
                    defaultPlayer1Attack,
                    defaultPlayer1NaturalSend),
                Player2 = CreateDefaultPlayerSetup(
                    GetControlDefaults(PlayerSlot.Player2),
                    defaultPlayer2Spawn,
                    defaultPlayer2Catalog,
                    defaultPlayer2Attack,
                    defaultPlayer2NaturalSend)
            };
        }

        public PlayerSlotControlDefaults GetControlDefaults(PlayerSlot slot) {
            if (defaultPlayerInputSettings != null) {
                return defaultPlayerInputSettings.GetControlDefaults(slot);
            }

            return slot == PlayerSlot.Player1
                ? PlayerSlotControlDefaults.Create(false, PlayerInputDeviceKind.Keyboard, 0)
                : PlayerSlotControlDefaults.Create(true, PlayerInputDeviceKind.Gamepad, 0);
        }

        static PlayerSlotSetup CreateDefaultPlayerSetup(
            PlayerSlotControlDefaults control,
            DiceSpawnSettings spawn,
            DiceCatalog catalog,
            PlayerAttackSettings attack,
            PlayerNaturalSendSettings naturalSend) {
            return PlayerSlotSetup.CreateDefault(
                control.IsAi,
                control.InputConfig,
                DiceSpawnSettingsData.FromTemplate(spawn),
                DiceCatalogData.FromTemplate(catalog),
                PlayerAttackSettingsData.FromTemplate(attack),
                PlayerNaturalSendSettingsData.FromTemplate(naturalSend));
        }

        public bool ContainsSpawnPreset(DiceSpawnSettings settings) {
            return TryGetSpawnPresetIndex(settings, out _);
        }

        public bool ContainsCatalogPreset(DiceCatalog catalog) {
            return TryGetCatalogPresetIndex(catalog, out _);
        }

        public bool TryGetSpawnPresetIndex(DiceSpawnSettings settings, out int index) {
            return TryGetPresetIndex(spawnPresets, settings, out index);
        }

        public bool TryGetCatalogPresetIndex(DiceCatalog catalog, out int index) {
            return TryGetPresetIndex(catalogPresets, catalog, out index);
        }

        public bool TryGetAttackPresetIndex(PlayerAttackSettings settings, out int index) {
            return TryGetPresetIndex(attackPresets, settings, out index);
        }

        public bool TryGetNaturalSendPresetIndex(PlayerNaturalSendSettings settings, out int index) {
            return TryGetPresetIndex(naturalSendPresets, settings, out index);
        }

        public DiceSpawnSettings GetSpawnPreset(int index) {
            return GetPreset(spawnPresets, index);
        }

        public DiceCatalog GetCatalogPreset(int index) {
            return GetPreset(catalogPresets, index);
        }

        public PlayerAttackSettings GetAttackPreset(int index) {
            return GetPreset(attackPresets, index);
        }

        public PlayerNaturalSendSettings GetNaturalSendPreset(int index) {
            return GetPreset(naturalSendPresets, index);
        }

        static bool TryGetPresetIndex<T>(T[] presets, T value, out int index) where T : Object {
            index = -1;
            if (presets == null || value == null) {
                return false;
            }

            for (var i = 0; i < presets.Length; i++) {
                if (presets[i] == value) {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        static T GetPreset<T>(T[] presets, int index) where T : Object {
            if (presets == null || index < 0 || index >= presets.Length) {
                return null;
            }

            return presets[index];
        }
    }
}
