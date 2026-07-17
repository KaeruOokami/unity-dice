using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public static class PlayerMotionGate
    {
        public static bool TryGetBlockReason(
            PlayerSlot player,
            PlayerMatchActionContext actionContext,
            DiceRegistry registry,
            Board board,
            Vector2Int standingCell,
            DiceController standingDice,
            out string reason) {
            reason = null;

            if (standingDice != null && standingDice.IsRolling) {
                reason = "standing-dice-rolling";
                return true;
            }

            if (actionContext != null && actionContext.AnyRollingForPlayer(player)) {
                reason = "action-dice-rolling";
                return true;
            }

            if (MotionConflictEvaluator.HasExternalRollingConflict(
                player,
                registry,
                board,
                actionContext,
                standingCell,
                standingDice)) {
                reason = "spatial-motion-conflict";
                return true;
            }

            return false;
        }

        public static bool BlocksJumpOrLiftStart(
            PlayerSlot player,
            PlayerMatchActionContext actionContext,
            DiceRegistry registry,
            Board board,
            Vector2Int standingCell,
            DiceController standingDice) {
            return TryGetBlockReason(
                player,
                actionContext,
                registry,
                board,
                standingCell,
                standingDice,
                out _);
        }

        public static bool BlocksPushContact(
            PlayerSlot player,
            PlayerMatchActionContext actionContext,
            DiceRegistry registry,
            Board board,
            Vector2Int standingCell,
            DiceController standingDice) {
            return BlocksJumpOrLiftStart(
                player,
                actionContext,
                registry,
                board,
                standingCell,
                standingDice);
        }
    }
}
