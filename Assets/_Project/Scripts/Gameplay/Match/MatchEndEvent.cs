using System;
using DiceGame.Config;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Fired when a match/round enters a terminal state (game over / round end).
    /// </summary>
    public readonly struct MatchEndEvent
    {
        public PlayerSlot? RoundWinner { get; }
        public bool IsStandardGameOver { get; }

        public MatchEndEvent(PlayerSlot? roundWinner, bool isStandardGameOver) {
            RoundWinner = roundWinner;
            IsStandardGameOver = isStandardGameOver;
        }
    }
}
