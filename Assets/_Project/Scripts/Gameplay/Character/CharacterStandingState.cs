using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public struct CharacterStandingState
    {
        public Vector2Int GridCell;
        public DiceStackTier Tier;
        public SurfaceLayer Layer;
        public DiceController Dice;

        public bool IsOnFloor => Layer == SurfaceLayer.Floor;

        public static CharacterStandingState OnFloor(Vector2Int gridCell) {
            return new CharacterStandingState {
                GridCell = gridCell,
                Tier = DiceStackTier.Bottom,
                Layer = SurfaceLayer.Floor,
                Dice = null
            };
        }

        public static CharacterStandingState OnDice(
            Vector2Int gridCell,
            DiceStackTier tier,
            DiceController dice) {
            return new CharacterStandingState {
                GridCell = gridCell,
                Tier = tier,
                Layer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom,
                Dice = dice
            };
        }
    }
}
