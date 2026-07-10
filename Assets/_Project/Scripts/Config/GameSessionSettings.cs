using UnityEngine;

namespace DiceGame.Config
{
    public enum GameMode
    {
        Single,
        Coop,
        Versus
    }

    [CreateAssetMenu(fileName = "GameSessionSettings", menuName = "Dice/Game Session Settings")]
    public sealed class GameSessionSettings : ScriptableObject
    {
        [SerializeField] GameMode gameMode = GameMode.Single;
        [SerializeField] VersusBoardSettings versusBoardSettings;

        public GameMode GameMode => gameMode;
        public VersusBoardSettings VersusBoardSettings => versusBoardSettings;

        public int RequiredPlayerCount =>
            gameMode == GameMode.Single ? 1 : 2;

        public bool TryValidate(PlayerInputSettings inputSettings, out string errorMessage)
        {
            if (gameMode == GameMode.Versus && versusBoardSettings == null)
            {
                errorMessage = "GameSessionSettings: Versus mode requires VersusBoardSettings.";
                return false;
            }

            if (inputSettings == null)
            {
                errorMessage = "GameSessionSettings: PlayerInputSettings is not assigned.";
                return false;
            }

            if (inputSettings.ActivePlayerCount != RequiredPlayerCount)
            {
                errorMessage =
                    $"GameSessionSettings: {gameMode} requires {RequiredPlayerCount} player(s), but PlayerInputSettings has {inputSettings.ActivePlayerCount}.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
