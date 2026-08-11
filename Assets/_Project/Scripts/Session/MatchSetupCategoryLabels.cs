using System;
using DiceGame.Config;

namespace DiceGame.Session
{
    static class MatchSetupCategoryLabels
    {
        public const string Shared = "Shared";
        public const string Control = "Control";
        public const string Spawn = "Spawn";
        public const string Catalog = "Catalog";
        public const string Attack = "Attack";
        public const string NaturalSend = "Natural Send";
        public const string Player = "Player";
        public const string Category = "Category";
        public const string PlayerSlotDropdown = "PlayerSlotDropdown";
        public const string CategoryDropdown = "CategoryDropdown";

        public static string[] GetCategoryLabels(GameMode mode) {
            return mode switch {
                GameMode.Versus => new[] {
                    Shared,
                    Control,
                    Spawn,
                    Catalog,
                    Attack,
                    NaturalSend
                },
                GameMode.Single => new[] {
                    Shared,
                    Control
                },
                GameMode.Coop => new[] {
                    Shared,
                    Control
                },
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }
    }
}
