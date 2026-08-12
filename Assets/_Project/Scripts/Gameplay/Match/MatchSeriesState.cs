using System;
using DiceGame.Config;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Versus first-to-N series score. Survives scene reload between rounds.
    /// </summary>
    public static class MatchSeriesState
    {
        public static bool IsActive { get; private set; }
        public static int WinsToWin { get; private set; } = 1;
        public static int Player1Wins { get; private set; }
        public static int Player2Wins { get; private set; }

        public static event Action Changed;

        public static void Begin(int winsToWin) {
            IsActive = true;
            WinsToWin = Math.Max(1, winsToWin);
            Player1Wins = 0;
            Player2Wins = 0;
            RaiseChanged();
        }

        public static void Clear() {
            IsActive = false;
            WinsToWin = 1;
            Player1Wins = 0;
            Player2Wins = 0;
            RaiseChanged();
        }

        /// <summary>
        /// Registers a round result. Draw leaves scores unchanged.
        /// Returns true when a player reached <see cref="WinsToWin"/>.
        /// </summary>
        public static bool RegisterRoundResult(
            PlayerSlot? roundWinner,
            out PlayerSlot? matchWinner) {
            matchWinner = null;
            if (!IsActive) {
                return false;
            }

            if (roundWinner == PlayerSlot.Player1) {
                Player1Wins++;
            } else if (roundWinner == PlayerSlot.Player2) {
                Player2Wins++;
            }

            RaiseChanged();

            if (Player1Wins >= WinsToWin) {
                matchWinner = PlayerSlot.Player1;
                return true;
            }

            if (Player2Wins >= WinsToWin) {
                matchWinner = PlayerSlot.Player2;
                return true;
            }

            return false;
        }

        public static int GetWins(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? Player1Wins : Player2Wins;
        }

        static void RaiseChanged() {
            Changed?.Invoke();
        }
    }
}
