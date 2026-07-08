using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement.Support
{
    public static class CharacterPlacementConversion
    {
        public static CharacterSupportState ToSupportState(CharacterPlacement placement) {
            if (placement.IsOnFloor) {
                return CharacterSupportState.OnFloor(placement.GridCell);
            }

            // Note: existing CharacterPlacement can represent a "virtual top" standing state by storing
            // Tier=Top while Dice is a bottom dice and there is no top on that cell.
            // In the new model this is represented as Level=2 with support=Dice(bottom, surfaceLevel=Top).
            if (placement.Dice != null) {
                var level = placement.Tier == DiceStackTier.Top ? 2 : 1;
                var surfaceLevel = placement.Tier == DiceStackTier.Top
                    ? DiceSurfaceLevel.Top
                    : DiceSurfaceLevel.Bottom;
                return CharacterSupportState.OnDice(
                    placement.GridCell,
                    level,
                    SupportRef.DiceSupport(placement.Dice, surfaceLevel));
            }

            // Defensive fallback: no dice reference but not on floor.
            return CharacterSupportState.Airborne(placement.GridCell);
        }

        public static CharacterPlacement ToLegacyPlacement(CharacterSupportState state, DiceController dice = null) {
            if (state.Support.Kind == SupportKind.Floor) {
                return CharacterPlacement.OnFloor(state.Cell);
            }

            if (state.Support.Kind == SupportKind.Dice) {
                var tier = state.Support.DiceSurfaceLevel == DiceSurfaceLevel.Top
                    ? DiceStackTier.Top
                    : DiceStackTier.Bottom;
                return CharacterPlacement.OnDice(state.Cell, tier, state.Support.Dice);
            }

            // Airborne has no legacy equivalent; treat as floor at current cell for now.
            return CharacterPlacement.OnFloor(state.Cell);
        }

        public static CharacterSupportState FromTransition(MovementTransition transition, Vector2Int toCell) {
            if (transition.TargetLevel == SurfaceHeightLevel.Floor) {
                return CharacterSupportState.OnFloor(toCell);
            }

            if (transition.TargetDice != null) {
                var isTop = transition.TargetLevel >= SurfaceHeightLevel.Top;
                var surfaceLevel = isTop ? DiceSurfaceLevel.Top : DiceSurfaceLevel.Bottom;
                var level = isTop ? 2 : 1;
                return CharacterSupportState.OnDice(
                    toCell,
                    level,
                    SupportRef.DiceSupport(transition.TargetDice, surfaceLevel));
            }

            return CharacterSupportState.Airborne(toCell);
        }
    }
}

