using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public sealed class PlacementService
    {
        readonly DiceRegistry diceRegistry;
        CharacterPlacement character;

        public PlacementService(DiceRegistry registry) {
            diceRegistry = registry;
        }

        public DiceRegistry Dice => diceRegistry;
        public CharacterPlacement Character => character;

        public void SetCharacterOnFloor(Vector2Int gridCell) {
            character = CharacterPlacement.OnFloor(gridCell);
        }

        public void SetCharacterOnDice(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            character = CharacterPlacement.OnDice(gridCell, tier, dice);
        }

        public void SetInitialCharacterPlacement(CharacterPlacement placement) {
            character = placement;
        }

        public bool TryGetCharacterStandingDice(out DiceController dice) {
            if (character.IsOnFloor || character.Dice == null) {
                dice = null;
                return false;
            }

            dice = character.Dice;
            return true;
        }
    }
}
