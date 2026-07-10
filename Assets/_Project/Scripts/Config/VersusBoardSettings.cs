using DiceGame.Config;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Config
{
    [System.Serializable]
    public struct PlayerBoardDefinition
    {
        [Min(1)]
        [SerializeField] int width;
        [Min(1)]
        [SerializeField] int height;
        [SerializeField] DiceSpawnSettings spawnSettings;
        [SerializeField] PlayerAttackSettings attackSettings;

        public PlayerBoardDefinition(
            int boardWidth,
            int boardHeight,
            DiceSpawnSettings spawn,
            PlayerAttackSettings attack) {
            width = boardWidth;
            height = boardHeight;
            spawnSettings = spawn;
            attackSettings = attack;
        }

        public int Width => Mathf.Max(1, width);
        public int Height => Mathf.Max(1, height);
        public DiceSpawnSettings SpawnSettings => spawnSettings;
        public PlayerAttackSettings AttackSettings => attackSettings;
    }

    [CreateAssetMenu(fileName = "VersusBoardSettings", menuName = "Dice/Versus Board Settings")]
    public sealed class VersusBoardSettings : ScriptableObject
    {
        [SerializeField] PlayerBoardDefinition player1 = new(4, 6, null, null);
        [SerializeField] PlayerBoardDefinition player2 = new(4, 6, null, null);

        public PlayerBoardDefinition Player1 => player1;
        public PlayerBoardDefinition Player2 => player2;

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

        public bool TryValidate(out string errorMessage)
        {
            if (player1.SpawnSettings == null || player2.SpawnSettings == null)
            {
                errorMessage = "VersusBoardSettings: Each player requires DiceSpawnSettings.";
                return false;
            }

            if (player1.AttackSettings == null || player2.AttackSettings == null)
            {
                errorMessage = "VersusBoardSettings: Each player requires PlayerAttackSettings.";
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

            errorMessage = null;
            return true;
        }
    }
}
