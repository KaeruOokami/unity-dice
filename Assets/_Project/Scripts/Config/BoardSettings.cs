using DiceGame.Grid;
using UnityEngine;
using UnityEngine.Serialization;

namespace DiceGame.Config
{
    public enum VersusInitialDicePlacementMode
    {
        Independent,
        Mirrored
    }

    [System.Serializable]
    public struct PlayerBoardDefinition
    {
        [Min(1)]
        [SerializeField] int width;
        [Min(1)]
        [SerializeField] int height;
        [SerializeField] DiceSpawnSettings spawnSettings;
        [SerializeField] DiceCatalog diceCatalog;
        [SerializeField] PlayerAttackSettings attackSettings;
        [SerializeField] PlayerNaturalSendSettings naturalSendSettings;

        public PlayerBoardDefinition(
            int boardWidth,
            int boardHeight,
            DiceSpawnSettings spawn,
            DiceCatalog catalog,
            PlayerAttackSettings attack,
            PlayerNaturalSendSettings naturalSend) {
            width = boardWidth;
            height = boardHeight;
            spawnSettings = spawn;
            diceCatalog = catalog;
            attackSettings = attack;
            naturalSendSettings = naturalSend;
        }

        public int Width => Mathf.Max(1, width);
        public int Height => Mathf.Max(1, height);
        public DiceSpawnSettings SpawnSettings => spawnSettings;
        public DiceCatalog DiceCatalog => diceCatalog;
        public PlayerAttackSettings AttackSettings => attackSettings;
        public PlayerNaturalSendSettings NaturalSendSettings => naturalSendSettings;
    }

    [CreateAssetMenu(fileName = "BoardSettings", menuName = "Dice/Board Settings")]
    public sealed class BoardSettings : ScriptableObject, IBoardSettings
    {
        [Header("Single")]
        [Min(1)]
        [SerializeField] int singleWidth = 7;
        [Min(1)]
        [SerializeField] int singleHeight = 7;

        [Header("Shared Initial Dice (1P / 2P)")]
        [Min(1)]
        [SerializeField] int sharedInitialDiceCount = 15;

        [Header("Match Series")]
        [Min(1)]
        [SerializeField] int winsToWin = 2;
        [Min(0f)]
        [SerializeField] float roundEndDelaySeconds = 2f;

        [Header("Versus")]
        [SerializeField] PlayerBoardDefinition player1 = new(4, 6, null, null, null, null);
        [SerializeField] PlayerBoardDefinition player2 = new(4, 6, null, null, null, null);
        [SerializeField] VersusInitialDicePlacementMode initialDicePlacementMode =
            VersusInitialDicePlacementMode.Mirrored;
        [FormerlySerializedAs("attackQueueUiSettings")]
        [SerializeField] AttackQueueSettings attackQueueSettings;
        [SerializeField] JumboDiceSettings jumboDiceSettings;

        public int SingleWidth => Mathf.Max(1, singleWidth);
        public int SingleHeight => Mathf.Max(1, singleHeight);
        public int SharedInitialDiceCount => Mathf.Max(1, sharedInitialDiceCount);
        public int WinsToWin => Mathf.Max(1, winsToWin);
        public float RoundEndDelaySeconds => Mathf.Max(0f, roundEndDelaySeconds);
        public PlayerBoardDefinition Player1 => player1;
        public PlayerBoardDefinition Player2 => player2;
        public VersusInitialDicePlacementMode InitialDicePlacementMode => initialDicePlacementMode;
        public AttackQueueSettings AttackQueueSettings => attackQueueSettings;
        public JumboDiceSettings JumboDiceSettings => jumboDiceSettings;

        void OnValidate() {
            singleWidth = Mathf.Max(1, singleWidth);
            singleHeight = Mathf.Max(1, singleHeight);
            winsToWin = Mathf.Max(1, winsToWin);
            roundEndDelaySeconds = Mathf.Max(0f, roundEndDelaySeconds);
            SyncSharedInitialDiceCountToPlayerSpawns();
#if UNITY_EDITOR
            if (player1.SpawnSettings != null) {
                UnityEditor.EditorUtility.SetDirty(player1.SpawnSettings);
            }

            if (player2.SpawnSettings != null) {
                UnityEditor.EditorUtility.SetDirty(player2.SpawnSettings);
            }
#endif
        }

        public void SyncSharedInitialDiceCountToPlayerSpawns() {
            var count = SharedInitialDiceCount;
            if (player1.SpawnSettings != null) {
                player1.SpawnSettings.SetInitialDiceCount(count);
            }

            if (player2.SpawnSettings != null) {
                player2.SpawnSettings.SetInitialDiceCount(count);
            }
        }

        public VersusArenaLayout CreateLayout()
        {
            return new VersusArenaLayout(player1.Width, player1.Height, player2.Width, player2.Height);
        }

        public DiceSpawnSettings GetSpawnSettings(PlayerSlot slot)
        {
            var definition = slot == PlayerSlot.Player1 ? player1 : player2;
            return definition.SpawnSettings;
        }

        public PlayerAttackSettings GetAttackSettings(PlayerSlot slot)
        {
            var definition = slot == PlayerSlot.Player1 ? player1 : player2;
            return definition.AttackSettings;
        }

        public DiceCatalog GetDiceCatalog(PlayerSlot slot)
        {
            var definition = slot == PlayerSlot.Player1 ? player1 : player2;
            return definition.DiceCatalog;
        }

        public PlayerNaturalSendSettings GetNaturalSendSettings(PlayerSlot slot)
        {
            var definition = slot == PlayerSlot.Player1 ? player1 : player2;
            return definition.NaturalSendSettings;
        }

        public bool TryValidate(out string errorMessage)
        {
            SyncSharedInitialDiceCountToPlayerSpawns();

            if (player1.SpawnSettings == null || player2.SpawnSettings == null)
            {
                errorMessage = "BoardSettings: Each player requires DiceSpawnSettings.";
                return false;
            }

            if (player1.DiceCatalog == null || player2.DiceCatalog == null)
            {
                errorMessage = "BoardSettings: Each player requires DiceCatalog.";
                return false;
            }

            if (player1.SpawnSettings.InitialDiceCount != player2.SpawnSettings.InitialDiceCount)
            {
                errorMessage =
                    "BoardSettings: InitialDiceCount must be shared between Player1 and Player2.";
                return false;
            }

            if (initialDicePlacementMode == VersusInitialDicePlacementMode.Mirrored)
            {
                if (player1.Width != player2.Width || player1.Height != player2.Height)
                {
                    errorMessage =
                        "BoardSettings: Mirrored initial dice placement requires matching board sizes.";
                    return false;
                }
            }

            if (player1.AttackSettings == null || player2.AttackSettings == null)
            {
                errorMessage = "BoardSettings: Each player requires PlayerAttackSettings.";
                return false;
            }

            if (!player1.AttackSettings.TryValidate(out errorMessage))
            {
                return false;
            }

            if (!player2.AttackSettings.TryValidate(out errorMessage))
            {
                return false;
            }

            if (player1.NaturalSendSettings != null
                && !player1.NaturalSendSettings.TryValidate(out errorMessage))
            {
                return false;
            }

            if (player2.NaturalSendSettings != null
                && !player2.NaturalSendSettings.TryValidate(out errorMessage))
            {
                return false;
            }

            if (jumboDiceSettings != null && !jumboDiceSettings.TryValidate(out errorMessage))
            {
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
