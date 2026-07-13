using System.Collections.Generic;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public sealed class DiceMatchOwnershipContext : MonoBehaviour
    {
        readonly Dictionary<DiceController, PlayerSlot> owners = new();
        readonly Dictionary<DiceController, PlayerSlot> tierFallSupportOwners = new();

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

        public void OnDiceRemoved(DiceController dice) {
            ClearOwner(dice);
            ClearTierFallSupportOwner(dice);
        }

        public void CaptureTierFallSupportOwner(DiceController fallenDice, DiceController removedBottom) {
            if (fallenDice == null) {
                return;
            }

            tierFallSupportOwners.Remove(fallenDice);
            if (removedBottom != null && TryGetOwner(removedBottom, out var supportOwner)) {
                tierFallSupportOwners[fallenDice] = supportOwner;
            }
        }

        public bool TryGetTierFallSupportOwner(DiceController fallenDice, out PlayerSlot owner) {
            owner = default;
            Debug.Log($"TryGetTierFallSupportOwner: fallenDice={fallenDice.name} owner={tierFallSupportOwners.TryGetValue(fallenDice, out owner)}");
            return fallenDice != null && tierFallSupportOwners.TryGetValue(fallenDice, out owner);
        }

        public void ClearTierFallSupportOwner(DiceController fallenDice) {
            if (fallenDice == null) {
                return;
            }

            tierFallSupportOwners.Remove(fallenDice);
        }
    }
}
