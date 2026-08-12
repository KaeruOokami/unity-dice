using System;

namespace DiceGame.Config
{
    public static class GameModeDisplayNames
    {
        public static string GetDisplayName(GameMode mode) {
            return mode switch {
                GameMode.Single => "Single",
                GameMode.Coop => "Coop",
                GameMode.Versus => "Versus",
                GameMode.Challenge => "Challenge",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }
    }
}
