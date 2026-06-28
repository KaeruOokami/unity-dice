using System;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public sealed class CharacterStandingController
    {
        Board board;
        DiceRegistry registry;
        Action endRollTracking;

        CharacterStandingState current;
        bool hasPendingStandingUpdate;
        Vector2Int pendingGridCell;
        DiceStackTier pendingTier;

        public CharacterStandingState Current => current;
        public DiceController CurrentDice => current.Dice;
        public bool IsOnFloor => current.IsOnFloor;
        public Vector2Int GridCell => current.GridCell;
        public DiceStackTier Tier => current.Tier;
        public SurfaceLayer Layer => current.Layer;
        public bool HasPendingStandingUpdate => hasPendingStandingUpdate;

        public event Action<DiceState> StandingDiceStateChanged;

        public void Configure(Board targetBoard, DiceRegistry targetRegistry, Action onEndRollTracking) {
            board = targetBoard;
            registry = targetRegistry;
            endRollTracking = onEndRollTracking;
        }

        public void SetInitialStanding(CharacterStandingState standing) {
            UnsubscribeDice();
            current = standing;
            if (standing.Dice != null) {
                SubscribeDice(standing.Dice);
            }
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
            current = CharacterStandingState.OnFloor(gridCell);
        }

        public void SetOnDice(Vector2Int gridCell, DiceStackTier tier, DiceController dice) {
            endRollTracking?.Invoke();
            current = CharacterStandingState.OnDice(gridCell, tier, dice);
            SubscribeDice(dice);
        }

        public void ApplyImmediateFromDiceState(DiceController dice) {
            if (dice == null) {
                return;
            }

            var state = dice.CurrentState;
            SetOnDice(state.GridPos, state.Tier, dice);
        }

        public void QueueDeferredStanding(Vector2Int toCell, DiceStackTier tier) {
            hasPendingStandingUpdate = true;
            pendingGridCell = toCell;
            pendingTier = tier;
        }

        public void CompleteDeferredStanding(DiceController dice) {
            if (!hasPendingStandingUpdate) {
                return;
            }

            hasPendingStandingUpdate = false;
            SetOnDice(pendingGridCell, pendingTier, dice);
        }

        public void ClearDeferredStanding() {
            hasPendingStandingUpdate = false;
        }

        public bool TryGetStandingDice(out DiceController dice) {
            dice = null;
            if (registry == null || current.IsOnFloor) {
                return false;
            }

            if (current.Layer == SurfaceLayer.Top) {
                if (registry.TryGetTopAt(current.GridCell, out dice)) {
                    return true;
                }

                if (current.Dice != null
                    && current.Dice.CurrentState.Tier == DiceStackTier.Bottom
                    && current.Dice.CurrentState.GridPos == current.GridCell) {
                    dice = current.Dice;
                    return true;
                }

                return false;
            }

            return registry.TryGetBottomAt(current.GridCell, out dice);
        }

        public void SyncStandingDiceCache() {
            if (!TryGetStandingDice(out var dice)) {
                if (current.Dice != null) {
                    UnsubscribeDice();
                    current = CharacterStandingState.OnFloor(current.GridCell);
                }

                return;
            }

            if (current.Dice != dice) {
                current = CharacterStandingState.OnDice(current.GridCell, current.Tier, dice);
                SubscribeDice(dice);
            }
        }

        public DiceController ResolveStandingDiceForMovement() {
            SyncStandingDiceCache();
            return current.Dice;
        }

        public void UnsubscribeAll() {
            UnsubscribeDice();
        }

        void SubscribeDice(DiceController dice) {
            UnsubscribeDice();
            current = new CharacterStandingState {
                GridCell = current.GridCell,
                Tier = current.Tier,
                Layer = current.Layer,
                Dice = dice
            };
            if (dice != null) {
                dice.StateChanged += OnDiceStateChanged;
            }
        }

        void UnsubscribeDice() {
            if (current.Dice != null) {
                current.Dice.StateChanged -= OnDiceStateChanged;
            }
        }

        void OnDiceStateChanged(DiceState state) {
            StandingDiceStateChanged?.Invoke(state);

            if (!current.IsOnFloor) {
                current = new CharacterStandingState {
                    GridCell = state.GridPos,
                    Tier = current.Tier,
                    Layer = current.Layer,
                    Dice = current.Dice
                };
            }

            if (TryGetStandingDice(out var standingDice)
                && standingDice == current.Dice
                && state.GridPos == current.GridCell
                && state.Tier != current.Tier) {
                SetOnDice(state.GridPos, state.Tier, standingDice);
            }
        }
    }
}
