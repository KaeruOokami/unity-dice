using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public static class GameWorldVisibility
    {
        public static void SetBoardVisible(Board board, bool visible) {
            if (board == null) {
                return;
            }

            board.gameObject.SetActive(visible);
        }
    }
}
