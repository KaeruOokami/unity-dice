using System;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Copied from <see cref="DiceController.TryBeginCarry"/> / <see cref="DiceController.TryPlaceAt"/>.
    /// Shared by production DiceController and Quantum <c>DiceBoardViewBinder</c>.
    /// </summary>
    public static class DiceCarryMotion
    {
        public static bool TryBeginCarry(
            DiceView diceView,
            Board board,
            DiceRegistry registry,
            DiceState fromState,
            Vector3 carryWorldTarget,
            Action onLogicalComplete,
            Action<float, Action> startLogicalBusy,
            Action clearLogicalBusyWithoutComplete)
        {
            if (diceView == null || diceView.DiceTransform == null || board == null)
            {
                return false;
            }

            // Logical from-position (not mid-lerp view transform) so both lockstep peers match.
            var fromWorld = diceView.GetAnchoredWorldPosition(fromState, board, registry);
            var transition = DiceTransition.FreeMove(fromWorld, carryWorldTarget, snapToGridOnComplete: false);
            startLogicalBusy(
                diceView.GetTransitionLogicalDuration(transition, board, registry),
                () =>
                {
                    // Still carried: do not snap back onto the vacated cell.
                    clearLogicalBusyWithoutComplete();
                    onLogicalComplete?.Invoke();
                });
            diceView.PlayTransition(transition, board, registry, null);
            return true;
        }

        public static bool TryPlaceAt(
            DiceView diceView,
            Board board,
            DiceRegistry registry,
            DiceState currentOrientationState,
            Vector2Int targetGrid,
            DiceStackTier targetTier,
            Vector3 fromWorld,
            Action onLogicalComplete,
            Action<float, Action> startLogicalBusy,
            Action finishLogicalBusy,
            Action<DiceState> commitPlacedState)
        {
            if (diceView == null || board == null)
            {
                return false;
            }

            var toState = new DiceState(
                targetGrid,
                currentOrientationState.Orientation,
                targetTier,
                currentOrientationState.Kind);
            var toWorld = diceView.GetAnchoredWorldPosition(toState, board, registry);
            var transition = DiceTransition.FreeMove(fromWorld, toWorld, snapToGridOnComplete: true, toState);

            startLogicalBusy(
                diceView.GetTransitionLogicalDuration(transition, board, registry),
                () =>
                {
                    commitPlacedState?.Invoke(toState);
                    finishLogicalBusy();
                    onLogicalComplete?.Invoke();
                });
            diceView.PlayTransition(transition, board, registry, null);
            return true;
        }

        /// <summary>
        /// Same FreeMove duration rule as <see cref="DiceView.GetTransitionLogicalDuration"/> for Lift/Place.
        /// Used by Quantum sim tick busy windows.
        /// </summary>
        public static float ResolveFreeMoveLogicalDuration(bool snapToGridOnComplete, float liftDuration, float placeDuration)
        {
            return snapToGridOnComplete ? placeDuration : liftDuration;
        }
    }
}
