using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public static class MatchAttackerResolver
    {
        public static bool TryResolveAttacker(
            IReadOnlyList<DiceController> cluster,
            MatchActionSnapshot action,
            IReadOnlyCollection<DiceController> participants,
            DiceMatchOwnershipContext ownershipContext,
            Board board,
            DiceController referenceDice,
            out PlayerSlot attacker) {
            attacker = default;
            if (cluster == null || cluster.Count == 0) {
                return false;
            }

            if (action != null
                && action.AllDice != null
                && action.AllDice.Count > 0
                && HasActionParticipant(cluster, action.AllDice)
                && TryResolveAttackerFromAction(action, cluster, out attacker)) {
                return true;
            }

            if (TryResolveFromErasingMembers(cluster, ownershipContext, out attacker)) {
                return true;
            }

            var dice = referenceDice ?? ResolveReferenceDice(cluster, participants);
            if (TryResolveFromBoardRegion(dice, board, ownershipContext, out attacker)) {
                return true;
            }

            Debug.LogError(
                "MatchAttackerResolver: Failed to resolve attacker. " +
                $"reference={(dice != null ? dice.name : "(none)")}");
            return false;
        }

        /// <summary>
        /// Resolves the attacker for a tier-fall match (reservation dice / one-erasure fall).
        /// The fallen dice never carries its own owner here: it must inherit either from the
        /// support (removed bottom) owner or from a sinking member of its match cluster.
        /// </summary>
        public static bool TryResolveAttackerForTierFall(
            IReadOnlyList<DiceController> cluster,
            DiceMatchOwnershipContext ownershipContext,
            DiceController fallenDice,
            out PlayerSlot attacker) {
            attacker = default;
            if (fallenDice == null) {
                return false;
            }

            if (ownershipContext != null
                && ownershipContext.TryGetTierFallSupportOwner(fallenDice, out var supportOwner)) {
                attacker = supportOwner;
                Debug.Log($"TryResolveAttackerForTierFall: resolved attacker={attacker} from support owner");
                return true;
            }

            if (cluster != null
                && cluster.Count > 0
                && TryResolveFromErasingMembers(cluster, ownershipContext, out attacker)) {
                Debug.Log($"TryResolveAttackerForTierFall: resolved attacker={attacker} from erasing members");
                return true;
            }

            Debug.LogError(
                "MatchAttackerResolver: Failed to resolve tier-fall attacker. " +
                $"fallen={fallenDice.name} cell={fallenDice.CurrentState.GridPos} " +
                "Expected a support owner or a sinking cluster member.");
            return false;
        }

        static bool HasActionParticipant(
            IReadOnlyList<DiceController> cluster,
            IReadOnlyCollection<DiceController> participants) {
            if (participants == null || participants.Count == 0) {
                return false;
            }

            foreach (var dice in cluster) {
                foreach (var participant in participants) {
                    if (dice == participant) {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool TryResolveFromErasingMembers(
            IReadOnlyList<DiceController> cluster,
            DiceMatchOwnershipContext ownershipContext,
            out PlayerSlot attacker) {
            attacker = default;
            if (ownershipContext == null || cluster == null) {
                return false;
            }

            PlayerSlot? resolved = null;
            for (var i = 0; i < cluster.Count; i++) {
                var dice = cluster[i];
                if (dice == null || !dice.IsErasing) {
                    continue;
                }

                if (!ownershipContext.TryGetOwner(dice, out var owner)) {
                    continue;
                }

                if (resolved.HasValue && resolved.Value != owner) {
                    Debug.LogError(
                        "MatchAttackerResolver: Conflicting erasing owners in cluster. " +
                        $"first={resolved.Value} next={owner}");
                    return false;
                }

                resolved = owner;
            }

            if (!resolved.HasValue) {
                return false;
            }

            attacker = resolved.Value;
            return true;
        }

        static bool TryResolveFromBoardRegion(
            DiceController dice,
            Board board,
            DiceMatchOwnershipContext ownershipContext,
            out PlayerSlot attacker) {
            attacker = default;
            if (dice == null || board == null) {
                return false;
            }

            if (board.IsVersusArena && board.VersusLayout != null) {
                attacker = board.VersusLayout.GetOwner(dice.CurrentState.GridPos);
                return true;
            }

            if (ownershipContext != null && ownershipContext.TryGetOwner(dice, out var owner)) {
                attacker = owner;
                return true;
            }

            attacker = PlayerSlot.Player1;
            return true;
        }

        static DiceController ResolveReferenceDice(
            IReadOnlyList<DiceController> cluster,
            IReadOnlyCollection<DiceController> participants) {
            if (participants != null) {
                foreach (var participant in participants) {
                    if (participant == null) {
                        continue;
                    }

                    for (var i = 0; i < cluster.Count; i++) {
                        if (cluster[i] == participant) {
                            return participant;
                        }
                    }
                }
            }

            return cluster[0];
        }

        static bool TryResolveAttackerFromAction(
            MatchActionSnapshot action,
            IReadOnlyList<DiceController> cluster,
            out PlayerSlot attacker) {
            attacker = default;
            if (action == null || cluster == null) {
                return false;
            }

            foreach (var slot in action.GetParticipatingPlayers()) {
                var actionDice = action.GetDiceFor(slot);
                for (var i = 0; i < actionDice.Count; i++) {
                    var dice = actionDice[i];
                    if (dice == null) {
                        continue;
                    }

                    for (var j = 0; j < cluster.Count; j++) {
                        if (cluster[j] == dice) {
                            attacker = slot;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
