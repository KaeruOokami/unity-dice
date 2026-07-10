using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Grid
{
    public sealed class VersusArenaLayout
    {
        public int Player1Width { get; }
        public int Player1Height { get; }
        public int Player2Width { get; }
        public int Player2Height { get; }
        public int PartitionX { get; }
        public int GlobalWidth { get; }
        public int GlobalHeight { get; }

        public VersusArenaLayout(int player1Width, int player1Height, int player2Width, int player2Height)
        {
            Player1Width = Mathf.Max(1, player1Width);
            Player1Height = Mathf.Max(1, player1Height);
            Player2Width = Mathf.Max(1, player2Width);
            Player2Height = Mathf.Max(1, player2Height);
            PartitionX = Player1Width;
            GlobalWidth = Player1Width + Player2Width;
            GlobalHeight = Mathf.Max(Player1Height, Player2Height);
        }

        public bool IsInsideGlobal(Vector2Int gridPos)
        {
            if (gridPos.x < 0 || gridPos.x >= GlobalWidth || gridPos.y < 0 || gridPos.y >= GlobalHeight)
            {
                return false;
            }

            return gridPos.x < PartitionX
                ? gridPos.y < Player1Height
                : gridPos.y < Player2Height;
        }

        public CellType GetCell(Vector2Int gridPos)
        {
            return IsInsideGlobal(gridPos) ? CellType.Floor : CellType.Wall;
        }

        public bool CrossesPartition(Vector2Int fromCell, Vector2Int toCell)
        {
            if (fromCell == toCell)
            {
                return false;
            }

            var fromPlayer1 = fromCell.x < PartitionX;
            var toPlayer1 = toCell.x < PartitionX;
            return fromPlayer1 != toPlayer1;
        }

        public PlayerSlot GetOwner(Vector2Int gridPos)
        {
            return gridPos.x < PartitionX ? PlayerSlot.Player1 : PlayerSlot.Player2;
        }

        public bool IsInsidePlayerRegion(PlayerSlot slot, Vector2Int gridPos)
        {
            return IsInsideGlobal(gridPos) && GetOwner(gridPos) == slot;
        }

        public void GetPlayerGridBounds(PlayerSlot slot, out Vector2Int minCell, out Vector2Int maxCell)
        {
            if (slot == PlayerSlot.Player1)
            {
                minCell = Vector2Int.zero;
                maxCell = new Vector2Int(Player1Width - 1, Player1Height - 1);
                return;
            }

            minCell = new Vector2Int(PartitionX, 0);
            maxCell = new Vector2Int(GlobalWidth - 1, Player2Height - 1);
        }
    }
}
