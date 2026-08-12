using UnityEngine;

namespace DiceGame.Config
{
    public enum GameMode
    {
        Single,
        Coop,
        Versus,
        Challenge
    }

    public static class GameModeRules
    {
        public static bool IsVersusLike(GameMode mode) {
            return mode == GameMode.Versus || mode == GameMode.Challenge;
        }
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

        public bool TryValidate(out string errorMessage)
        {
            if (GameModeRules.IsVersusLike(gameMode) && versusBoardSettings == null)
            {
                errorMessage = "GameSessionSettings: Versus-like modes require VersusBoardSettings.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
