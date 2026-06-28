using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public struct CharacterPlacement
    {
        public Vector2Int GridCell;
        public DiceStackTier Tier;
        public SurfaceLayer Layer;
        public DiceController Dice;

        public bool IsOnFloor => Layer == SurfaceLayer.Floor;

        public static CharacterPlacement OnFloor(Vector2Int gridCell) {
            return new CharacterPlacement {
                GridCell = gridCell,
                Tier = DiceStackTier.Bottom,
                Layer = SurfaceLayer.Floor,
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
                Layer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom,
                Dice = dice
            };
        }
    }
}
