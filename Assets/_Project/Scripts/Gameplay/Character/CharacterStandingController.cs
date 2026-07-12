using System;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using DiceGame.Placement.Support;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public sealed class CharacterStandingController
    {
        PlacementService placement;
        Action endRollTracking;
        DiceController subscribedDice;
        CharacterSupportState supportState;

        public CharacterSupportState SupportState => supportState;
        public SupportRef Support => supportState.Support;
        public int Level => supportState.Level;
        public bool IsAirborne => supportState.IsAirborne;
        public Vector2Int GridCell => supportState.Cell;

        public CharacterPlacement Current => CharacterPlacementConversion.ToLegacyPlacement(supportState);
        public DiceController CurrentDice =>
            supportState.Support.Kind == SupportKind.Dice ? supportState.Support.Dice : null;
        public bool IsOnFloor => supportState.Support.Kind == SupportKind.Floor;
        public DiceStackTier Tier => ResolveTier(supportState);

        public event Action<DiceState> StandingDiceStateChanged;

        public void Configure(PlacementService placementService, Action onEndRollTracking) {
            placement = placementService;
            endRollTracking = onEndRollTracking;
        }

        public void SetInitialStanding(CharacterPlacement standing) {
            ApplySupportState(CharacterPlacementConversion.ToSupportState(standing), syncPlacement: false);
            placement.SetInitialCharacterPlacement(standing);
            SubscribeDice(CurrentDice);
        }

        public void ApplyFromTransition(MovementTransition transition, Vector2Int toCell) {
            ApplySupportState(CharacterPlacementConversion.FromTransition(transition, toCell));
        }

        public void SetOnFloor(Vector2Int gridCell) {
            ApplySupportState(CharacterSupportState.OnFloor(gridCell));
        }

        public void SetOnDice(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            var surfaceLevel = tier == DiceStackTier.Top
                ? DiceSurfaceLevel.Top
                : DiceSurfaceLevel.Bottom;
            var level = tier == DiceStackTier.Top ? 2 : 1;
            ApplySupportState(CharacterSupportState.OnDice(
                gridCell,
                level,
                SupportRef.DiceSupport(dice, surfaceLevel)));
        }

        public void SetAirborne(Vector2Int gridCell) {
            ApplySupportState(CharacterSupportState.Airborne(gridCell));
        }

        /// <summary>
        /// Updates passability cell during jump without changing dice/floor support (pending landing).
        /// </summary>
        public void SetTraversalCellWithoutSupportChange(Vector2Int gridCell) {
            if (supportState.Support.Kind == SupportKind.Dice) {
                supportState = CharacterSupportState.OnDice(
                    gridCell,
                    supportState.Level,
                    supportState.Support);
                return;
            }

            if (supportState.Support.Kind == SupportKind.Floor) {
                supportState = CharacterSupportState.OnFloor(gridCell);
                return;
            }

            supportState = CharacterSupportState.Airborne(gridCell);
        }

        public void ApplySupportState(CharacterSupportState state, bool syncPlacement = true) {
            endRollTracking?.Invoke();
            UnsubscribeDice();
            supportState = state;

            if (state.IsAirborne) {
                return;
            }

            if (syncPlacement) {
                placement.ApplyCharacterSupportState(state);
            }

            if (state.Support.Kind == SupportKind.Dice) {
                SubscribeDice(state.Support.Dice);
            }
        }

        public void ApplyImmediateFromDiceState(DiceController dice) {
            if (dice == null) {
                return;
            }

            var state = dice.CurrentState;
            SetOnDice(state.GridPos, state.Tier, dice);
        }

        public bool TryGetStandingDice(out DiceController dice) {
            if (supportState.Support.Kind != SupportKind.Dice || supportState.Support.Dice == null) {
                dice = null;
                return false;
            }

            dice = supportState.Support.Dice;
            return true;
        }

        public DiceController ResolveStandingDiceForMovement() {
            return CurrentDice;
        }

        public void UnsubscribeAll() {
            UnsubscribeDice();
        }

        static DiceStackTier ResolveTier(CharacterSupportState state) {
            if (state.Support.Kind == SupportKind.Floor) {
                return DiceStackTier.Bottom;
            }

            if (state.Support.Kind == SupportKind.Dice) {
                return state.Support.DiceSurfaceLevel == DiceSurfaceLevel.Top
                    ? DiceStackTier.Top
                    : DiceStackTier.Bottom;
            }

            return DiceStackTier.Bottom;
        }

        void SubscribeDice(DiceController dice) {
            UnsubscribeDice();
            subscribedDice = dice;
            if (dice != null) {
                dice.StateChanged += OnDiceStateChanged;
            }
        }

        void UnsubscribeDice() {
            if (subscribedDice != null) {
                subscribedDice.StateChanged -= OnDiceStateChanged;
                subscribedDice = null;
            }
        }

        void OnDiceStateChanged(DiceState state) {
            StandingDiceStateChanged?.Invoke(state);

            if (supportState.Support.Kind != SupportKind.Dice || supportState.Support.Dice == null) {
                return;
            }

            if (supportState.Support.Dice != subscribedDice) {
                return;
            }

            if (state.GridPos != supportState.Cell) {
                return;
            }

            var surfaceLevel = state.Tier == DiceStackTier.Top
                ? DiceSurfaceLevel.Top
                : DiceSurfaceLevel.Bottom;
            if (surfaceLevel == supportState.Support.DiceSurfaceLevel) {
                return;
            }

            var level = surfaceLevel == DiceSurfaceLevel.Top ? 2 : 1;
            ApplySupportState(CharacterSupportState.OnDice(
                state.GridPos,
                level,
                SupportRef.DiceSupport(supportState.Support.Dice, surfaceLevel)));
        }
    }
}
