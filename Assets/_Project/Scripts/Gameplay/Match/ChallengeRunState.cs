using System;
using DiceGame.Config;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Challenge gauntlet progress. Survives scene reload between matches.
    /// </summary>
    public static class ChallengeRunState
    {
        static PlayerAttackSettingsData[] opponentAttacks = Array.Empty<PlayerAttackSettingsData>();

        public static bool IsActive { get; private set; }
        public static int CurrentMatchIndex { get; private set; }
        public static int MatchCount { get; private set; }
        public static int DisplayMatchNumber => CurrentMatchIndex + 1;
        public static PlayerAttackSettingsData PlayerAttack { get; private set; }

        public static event Action Changed;

        public static void Begin(
            PlayerAttackSettingsData playerAttack,
            PlayerAttackSettingsData[] opponentsByMatch) {
            if (opponentsByMatch == null || opponentsByMatch.Length < 1) {
                Clear();
                return;
            }

            IsActive = true;
            CurrentMatchIndex = 0;
            MatchCount = opponentsByMatch.Length;
            PlayerAttack = playerAttack;
            opponentAttacks = new PlayerAttackSettingsData[opponentsByMatch.Length];
            Array.Copy(opponentsByMatch, opponentAttacks, opponentsByMatch.Length);
            RaiseChanged();
        }

        public static void Clear() {
            IsActive = false;
            CurrentMatchIndex = 0;
            MatchCount = 0;
            PlayerAttack = default;
            opponentAttacks = Array.Empty<PlayerAttackSettingsData>();
            RaiseChanged();
        }

        public static bool TryGetCurrentOpponentAttack(out PlayerAttackSettingsData attack) {
            attack = default;
            if (!IsActive || CurrentMatchIndex < 0 || CurrentMatchIndex >= opponentAttacks.Length) {
                return false;
            }

            attack = opponentAttacks[CurrentMatchIndex];
            return true;
        }

        /// <summary>
        /// Advances to the next opponent. Returns false when the gauntlet is cleared.
        /// </summary>
        public static bool TryAdvanceToNextMatch(out PlayerAttackSettingsData nextOpponentAttack) {
            nextOpponentAttack = default;
            if (!IsActive) {
                return false;
            }

            if (CurrentMatchIndex + 1 >= MatchCount) {
                return false;
            }

            CurrentMatchIndex++;
            nextOpponentAttack = opponentAttacks[CurrentMatchIndex];
            RaiseChanged();
            return true;
        }

        static void RaiseChanged() {
            Changed?.Invoke();
        }
    }
}
