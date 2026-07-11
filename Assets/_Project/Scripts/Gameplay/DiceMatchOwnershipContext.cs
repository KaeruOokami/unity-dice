using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public sealed class DiceMatchOwnershipContext : MonoBehaviour
    {
        readonly Dictionary<DiceController, PlayerSlot> owners = new();
        readonly Dictionary<DiceController, PlayerSlot> reservationSinkingOwners = new();

        DiceRegistry registry;

        public void Configure(DiceRegistry targetRegistry) {
            registry = targetRegistry;
        }

        public bool TryGetOwner(DiceController dice, out PlayerSlot owner) {
            owner = default;
            return dice != null && owners.TryGetValue(dice, out owner);
        }

        public void SetOwner(DiceController dice, PlayerSlot owner) {
            if (dice == null) {
                return;
            }

            owners[dice] = owner;
        }

        public void ClearOwner(DiceController dice) {
            if (dice == null) {
                return;
            }

            owners.Remove(dice);
        }

        public void SyncReservationForTop(DiceController topDice) {
            if (topDice == null || registry == null) {
                return;
            }

            if (topDice.CurrentState.Tier != DiceStackTier.Top) {
                return;
            }

            var grid = topDice.CurrentState.GridPos;
            if (!registry.TryGetBottomAt(grid, out var sinkingBottom)
                || sinkingBottom == null
                || sinkingBottom == topDice
                || !sinkingBottom.IsSinkErasing) {
                UnregisterReservation(topDice);
                return;
            }

            if (!TryGetOwner(sinkingBottom, out var sinkingOwner)) {
                UnregisterReservation(topDice);
                return;
            }

            reservationSinkingOwners[topDice] = sinkingOwner;
        }

        public bool IsReserved(DiceController topDice) {
            return topDice != null && reservationSinkingOwners.ContainsKey(topDice);
        }

        public bool TryGetReservedSinkingOwner(DiceController topDice, out PlayerSlot sinkingOwner) {
            sinkingOwner = default;
            return topDice != null && reservationSinkingOwners.TryGetValue(topDice, out sinkingOwner);
        }

        public void UnregisterReservation(DiceController topDice) {
            if (topDice == null) {
                return;
            }

            reservationSinkingOwners.Remove(topDice);
        }

        public bool ShouldEvaluateTierFall(DiceController fallenDice) {
            if (fallenDice == null) {
                return false;
            }

            if (IsReserved(fallenDice)) {
                return true;
            }

            return TryGetOwner(fallenDice, out _);
        }

        public bool TryResolveTierFallAttacker(DiceController fallenDice, out PlayerSlot attacker) {
            attacker = default;
            if (fallenDice == null) {
                return false;
            }

            if (TryGetReservedSinkingOwner(fallenDice, out attacker)) {
                return true;
            }

            if (TryGetOwner(fallenDice, out attacker)) {
                return true;
            }

            Debug.LogError(
                $"DiceMatchOwnershipContext: Tier-fall match has no attacker. fallen={fallenDice.name}");
            return false;
        }

        public void OnDiceRemoved(DiceController dice) {
            if (dice == null) {
                return;
            }

            ClearOwner(dice);
            UnregisterReservation(dice);
        }
    }
}
