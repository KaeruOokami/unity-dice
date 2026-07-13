using DiceGame.Config;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public static class PlayerMotionGate
    {
        public static bool BlocksJumpOrLiftStart(
            PlayerSlot player,
            PlayerMatchActionContext actionContext,
            DiceRegistry registry,
            Board board,
            Vector2Int standingCell,
            DiceController standingDice) {
            if (standingDice != null && standingDice.IsRolling) {
                return true;
            }

            if (actionContext != null && actionContext.AnyRollingForPlayer(player)) {
                return true;
            }

            return MotionConflictEvaluator.HasExternalRollingConflict(
                player,
                registry,
                board,
                actionContext,
                standingCell,
                standingDice);
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
