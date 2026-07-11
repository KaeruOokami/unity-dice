using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class PlayerMatchActionContext : MonoBehaviour
    {
        readonly HashSet<DiceController> actionDice = new();
        readonly Dictionary<DiceController, PlayerSlot> actionDiceOwners = new();

        DiceRegistry registry;
        DiceMatchOwnershipContext ownershipContext;

        public event Action<MatchActionSnapshot> ActionCompleted;

        public void Configure(DiceRegistry targetRegistry, DiceMatchOwnershipContext targetOwnershipContext) {
            registry = targetRegistry;
            ownershipContext = targetOwnershipContext;
        }

        public void RegisterActionDice(DiceController dice, PlayerSlot owner) {
            if (dice == null || dice.IsSpawning) {
                return;
            }

            actionDice.Add(dice);
            actionDiceOwners[dice] = owner;
            ownershipContext?.SetOwner(dice, owner);
        }

        public void RegisterActionDice(IReadOnlyList<DiceController> diceList, PlayerSlot owner) {
            if (diceList == null) {
                return;
            }

            for (var i = 0; i < diceList.Count; i++) {
                RegisterActionDice(diceList[i], owner);
            }
        }

        public bool IsInCurrentAction(DiceController dice) {
            return dice != null && actionDice.Contains(dice);
        }

        public void NotifyParticipantMoveCompleted() {
            TryCompleteAction();
        }

        void TryCompleteAction() {
            if (actionDice.Count == 0) {
                return;
            }

            if (registry != null && (registry.AnyRolling() || registry.AnyCarried())) {
                return;
            }

            var allDice = new List<DiceController>(actionDice);
            var diceByPlayer = new Dictionary<PlayerSlot, List<DiceController>>();

            foreach (var dice in actionDice) {
                if (!actionDiceOwners.TryGetValue(dice, out var owner)) {
                    continue;
                }

                if (!diceByPlayer.TryGetValue(owner, out var ownedDice)) {
                    ownedDice = new List<DiceController>();
                    diceByPlayer[owner] = ownedDice;
                }

                ownedDice.Add(dice);
            }

            actionDice.Clear();
            actionDiceOwners.Clear();
            ActionCompleted?.Invoke(new MatchActionSnapshot(allDice, diceByPlayer));
        }

        public static bool IsActionParticipationMove(DiceState from, DiceState to) {
            return from.GridPos != to.GridPos || from.Tier != to.Tier;
        }
    }
}
