using System;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class PlayerMatchActionContext : MonoBehaviour
    {
        readonly HashSet<DiceController> actionDice = new();

        DiceRegistry registry;

        public event Action<IReadOnlyCollection<DiceController>> ActionCompleted;

        public void Configure(DiceRegistry targetRegistry) {
            registry = targetRegistry;
        }

        public void RegisterActionDice(DiceController dice) {
            if (dice == null || dice.IsSpawning) {
                return;
            }

            actionDice.Add(dice);
        }

        public void RegisterActionDice(IReadOnlyList<DiceController> diceList) {
            if (diceList == null) {
                return;
            }

            for (var i = 0; i < diceList.Count; i++) {
                RegisterActionDice(diceList[i]);
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

            var snapshot = new List<DiceController>(actionDice);
            actionDice.Clear();
            ActionCompleted?.Invoke(snapshot);
        }

        public static bool IsActionParticipationMove(DiceState from, DiceState to) {
            return from.GridPos != to.GridPos || from.Tier != to.Tier;
        }
    }
}
