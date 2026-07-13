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
        readonly List<PlayerSlot> pendingPlayersScratch = new();

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

        public bool AnyRollingForPlayer(PlayerSlot player) {
            foreach (var dice in actionDice) {
                if (dice == null || !dice.IsRolling) {
                    continue;
                }

                if (actionDiceOwners.TryGetValue(dice, out var owner) && owner == player) {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetActionOwner(DiceController dice, out PlayerSlot owner) {
            return actionDiceOwners.TryGetValue(dice, out owner);
        }

        public bool AnyBusyActionDiceIntersectingCells(HashSet<Vector2Int> cells) {
            if (cells == null || cells.Count == 0) {
                return false;
            }

            foreach (var dice in actionDice) {
                if (dice == null || (!dice.IsRolling && !dice.IsCarried)) {
                    continue;
                }

                if (dice.IsCarried) {
                    continue;
                }

                if (cells.Contains(dice.CurrentState.GridPos)) {
                    return true;
                }
            }

            return false;
        }

        public void NotifyParticipantMoveCompleted(PlayerSlot player) {
            TryCompleteActionForPlayer(player);
        }

        public void NotifyParticipantMoveCompleted() {
            CollectPlayersWithPendingActions(pendingPlayersScratch);
            for (var i = 0; i < pendingPlayersScratch.Count; i++) {
                TryCompleteActionForPlayer(pendingPlayersScratch[i]);
            }
        }

        public void NotifyParticipantMoveCompleted(DiceController participant) {
            if (participant != null
                && TryGetActionOwner(participant, out var owner)) {
                TryCompleteActionForPlayer(owner);
                return;
            }

            NotifyParticipantMoveCompleted();
        }

        void TryCompleteActionForPlayer(PlayerSlot player) {
            if (!TryCollectActionDiceForPlayer(player, out var playerDice) || playerDice.Count == 0) {
                return;
            }

            for (var i = 0; i < playerDice.Count; i++) {
                var dice = playerDice[i];
                if (dice != null && (dice.IsRolling || dice.IsCarried)) {
                    return;
                }
            }

            for (var i = 0; i < playerDice.Count; i++) {
                var dice = playerDice[i];
                actionDice.Remove(dice);
                actionDiceOwners.Remove(dice);
            }

            var diceByPlayer = new Dictionary<PlayerSlot, List<DiceController>> {
                [player] = playerDice
            };
            ActionCompleted?.Invoke(new MatchActionSnapshot(playerDice, diceByPlayer));
        }

        void CollectPlayersWithPendingActions(List<PlayerSlot> players) {
            players.Clear();
            foreach (var pair in actionDiceOwners) {
                if (!players.Contains(pair.Value)) {
                    players.Add(pair.Value);
                }
            }
        }

        bool TryCollectActionDiceForPlayer(PlayerSlot player, out List<DiceController> playerDice) {
            playerDice = new List<DiceController>();
            foreach (var dice in actionDice) {
                if (actionDiceOwners.TryGetValue(dice, out var owner) && owner == player) {
                    playerDice.Add(dice);
                }
            }

            return playerDice.Count > 0;
        }

        public static bool IsActionParticipationMove(DiceState from, DiceState to) {
            return from.GridPos != to.GridPos || from.Tier != to.Tier;
        }
    }
}
