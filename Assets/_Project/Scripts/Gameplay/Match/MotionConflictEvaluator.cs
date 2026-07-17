using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public static class MotionConflictEvaluator
    {
        static readonly Direction[] CardinalDirections = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        public static HashSet<Vector2Int> CollectAdjacentInterestCells(Vector2Int center) {
            var cells = new HashSet<Vector2Int> { center };
            for (var i = 0; i < CardinalDirections.Length; i++) {
                cells.Add(center + CardinalDirections[i].ToGridDelta());
            }

            return cells;
        }

        public static bool HasExternalRollingConflict(
            PlayerSlot player,
            DiceRegistry registry,
            Board board,
            PlayerMatchActionContext actionContext,
            Vector2Int standingCell,
            DiceController standingDice) {
            if (registry == null) {
                return false;
            }

            var interest = CollectAdjacentInterestCells(standingCell);
            foreach (var dice in registry.AllDice) {
                if (!IsExternalRollingDice(dice, player, actionContext, standingDice)) {
                    continue;
                }

                var cell = dice.CurrentState.GridPos;
                if (!interest.Contains(cell)) {
                    continue;
                }

                if (board != null
                    && board.IsVersusArena
                    && !board.VersusLayout.IsInsidePlayerRegion(player, cell)) {
                    continue;
                }

                return true;
            }

            return false;
        }

        public static bool BlocksTierFallEvaluation(
            DiceController fallenDice,
            DiceRegistry registry,
            PlayerMatchActionContext actionContext) {
            if (fallenDice == null || registry == null) {
                return false;
            }

            var interest = CollectAdjacentInterestCells(fallenDice.CurrentState.GridPos);
            if (actionContext != null && actionContext.AnyBusyActionDiceIntersectingCells(interest)) {
                return true;
            }

            foreach (var dice in registry.AllDice) {
                if (dice == null || !dice.IsRolling) {
                    continue;
                }

                if (interest.Contains(dice.CurrentState.GridPos)) {
                    return true;
                }
            }

            return false;
        }

        static bool IsExternalRollingDice(
            DiceController dice,
            PlayerSlot player,
            PlayerMatchActionContext actionContext,
            DiceController standingDice) {
            if (dice == null || !dice.IsRolling) {
                return false;
            }

            if (dice == standingDice) {
                return false;
            }

            if (actionContext != null
                && actionContext.IsInCurrentAction(dice)
                && actionContext.TryGetActionOwner(dice, out var owner)
                && owner == player) {
                return false;
            }

            return true;
        }
    }
}
