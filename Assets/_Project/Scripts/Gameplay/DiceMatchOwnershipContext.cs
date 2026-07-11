using System.Collections.Generic;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public sealed class DiceMatchOwnershipContext : MonoBehaviour
    {
        readonly Dictionary<DiceController, PlayerSlot> owners = new();

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
        }
    }
}
