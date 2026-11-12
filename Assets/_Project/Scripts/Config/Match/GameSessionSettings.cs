using UnityEngine;
using UnityEngine.Serialization;

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

    [CreateAssetMenu(fileName = "GameSessionSettings", menuName = "Dice/Match/Game Session Settings")]
    public sealed class GameSessionSettings : ScriptableObject
    {
        [SerializeField] GameMode gameMode = GameMode.Single;
        [FormerlySerializedAs("versusBoardSettings")]
        [SerializeField] BoardSettings boardSettings;
        [SerializeField] MatchSeriesSettings matchSeriesSettings;

        public GameMode GameMode => gameMode;
        public BoardSettings BoardSettings => boardSettings;
        public MatchSeriesSettings MatchSeriesSettings => matchSeriesSettings;

        public int RequiredPlayerCount =>
            gameMode == GameMode.Single ? 1 : 2;

        public bool TryValidate(out string errorMessage)
        {
            if (boardSettings == null)
            {
                errorMessage = "GameSessionSettings: BoardSettings is required.";
                return false;
            }

            if (matchSeriesSettings == null)
            {
                errorMessage = "GameSessionSettings: MatchSeriesSettings is required.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
