using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public struct CharacterPlacement
    {
        public Vector2Int GridCell;
        public DiceStackTier Tier;
        public int Level;
        public DiceController Dice;

        public bool IsOnFloor => Level == SurfaceHeightLevel.Floor;

        public static CharacterPlacement OnFloor(Vector2Int gridCell) {
            return new CharacterPlacement {
                GridCell = gridCell,
                Tier = DiceStackTier.Bottom,
                Level = SurfaceHeightLevel.Floor,
                Dice = null
            };
        }

        public static CharacterPlacement OnDice(
            Vector2Int gridCell,
            DiceStackTier tier,
            DiceController dice) {
            return new CharacterPlacement {
                GridCell = gridCell,
                Tier = tier,
                Level = SurfaceHeightLevel.FromDiceStackTier(tier),
                Dice = dice
            };
        }
    }
}
