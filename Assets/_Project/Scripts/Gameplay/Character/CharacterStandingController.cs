using System;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public sealed class CharacterStandingController
    {
        PlacementService placement;
        Action endRollTracking;
        DiceController subscribedDice;

        public CharacterPlacement Current => placement.Character;
        public DiceController CurrentDice => placement.Character.Dice;
        public bool IsOnFloor => placement != null && placement.Character.IsOnFloor;
        public Vector2Int GridCell => placement.Character.GridCell;
        public DiceStackTier Tier => placement.Character.Tier;
        public SurfaceLayer Layer => placement.Character.Layer;

        public event Action<DiceState> StandingDiceStateChanged;

        public void Configure(PlacementService placementService, Action onEndRollTracking) {
            placement = placementService;
            endRollTracking = onEndRollTracking;
        }

        public void SetInitialStanding(CharacterPlacement standing) {
            UnsubscribeDice();
            placement.SetInitialCharacterPlacement(standing);
            SubscribeDice(standing.Dice);
        }

        public void ApplyFromTransition(MovementTransition transition, Vector2Int toCell) {
            if (transition.TargetLayer == SurfaceLayer.Floor) {
                SetOnFloor(toCell);
                return;
            }

            if (transition.TargetDice != null) {
                var tier = transition.TargetLayer == SurfaceLayer.Top
                    ? DiceStackTier.Top
                    : DiceStackTier.Bottom;
                SetOnDice(toCell, tier, transition.TargetDice);
            }
        }

        public void SetOnFloor(Vector2Int gridCell) {
            endRollTracking?.Invoke();
            UnsubscribeDice();
            placement.SetCharacterOnFloor(gridCell);
        }

        public void SetOnDice(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            endRollTracking?.Invoke();
            UnsubscribeDice();
            placement.SetCharacterOnDice(gridCell, tier, dice);
            SubscribeDice(dice);
        }

        public void ApplyImmediateFromDiceState(DiceController dice) {
            if (dice == null) {
                return;
            }

            var state = dice.CurrentState;
            SetOnDice(state.GridPos, state.Tier, dice);
        }

        public bool TryGetStandingDice(out DiceController dice) {
            return placement.TryGetCharacterStandingDice(out dice);
        }

        public DiceController ResolveStandingDiceForMovement() {
            return placement.Character.Dice;
        }

        public void UnsubscribeAll() {
            UnsubscribeDice();
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

            var current = placement.Character;
            if (current.IsOnFloor || current.Dice == null) {
                return;
            }

            if (current.Dice != subscribedDice) {
                return;
            }

            if (state.GridPos == current.GridCell && state.Tier != current.Tier) {
                SetOnDice(state.GridPos, state.Tier, current.Dice);
            }
        }
    }
}
