using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement.Support;
using UnityEngine;

namespace DiceGame.Placement
{
    public sealed class PlacementService
    {
        readonly DiceRegistry diceRegistry;
        readonly SurfaceQuery surfaceQuery;
        readonly MovementTransitionEvaluator passability;
        CharacterPlacement character;

        public PlacementService(DiceRegistry registry, Board board, HeightStepLimits stepLimits) {
            diceRegistry = registry;
            surfaceQuery = new SurfaceQuery(board, registry);
            passability = new MovementTransitionEvaluator(board, registry, surfaceQuery, stepLimits);
        }

        public DiceRegistry Dice => diceRegistry;
        public CharacterPlacement Character => character;
        public SurfaceQuery Surfaces => surfaceQuery;
        public MovementTransitionEvaluator Passability => passability;

        public void SetCharacterOnFloor(Vector2Int gridCell) {
            character = CharacterPlacement.OnFloor(gridCell);
        }

        public void SetCharacterOnDice(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            character = CharacterPlacement.OnDice(gridCell, tier, dice);
        }

        public void ApplyCharacterSupportState(CharacterSupportState state) {
            if (state.IsAirborne) {
                return;
            }

            character = CharacterPlacementConversion.ToLegacyPlacement(state);
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
