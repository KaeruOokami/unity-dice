using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Builds dice-to-dice height transfer transitions for a concrete target.
    /// </summary>
    public static class HeightTransferTargetEvaluator
    {
        public static bool TryEvaluateToTarget(
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            bool isJumping,
            HeightReachEvaluation reach,
            DiceRegistry registry,
            DiceController target,
            bool dissolveDescentHoldOnly,
            out MovementTransition transition,
            out string rejectReason) {
            transition = default;
            rejectReason = null;
            if (target == null) {
                rejectReason = "no-transfer-target";
                return false;
            }

            if (!ExpandedFootprintWalkPolicy.OccupiesCell(target, toCell)) {
                rejectReason =
                    $"target-cell-mismatch target={FormatDice(target)} " +
                    $"targetCell=({target.CurrentState.GridPos.x},{target.CurrentState.GridPos.y})";
                return false;
            }

            if (JumpDiceTransferPolicy.ShouldBlockDiceToDiceTransfer(isJumping, standingDice, target)) {
                rejectReason = "jump-ice-dice-to-dice-transfer-blocked";
                return false;
            }

            var targetLevel = target.Capabilities.HasExpandedFootprint
                ? ExpandedFootprintWalkPolicy.ResolveStandingLevel(target, fromLevel)
                : target.CurrentState.Tier == DiceStackTier.Top
                    ? SurfaceHeightLevel.Top
                    : SurfaceHeightLevel.Bottom;
            var targetSurface = BoardSurface.FromDice(toCell, targetLevel, target);

            var allowDescentOnly = isJumping
                && targetSurface.SurfaceWorldY < fromSurface.SurfaceWorldY - 0.001f;

            var evaluated = dissolveDescentHoldOnly
                ? WalkTransferPolicy.TryEvaluateDissolveDescentHold(
                    target,
                    fromLevel,
                    registry,
                    fromSurface,
                    targetSurface,
                    standingDice,
                    reach,
                    out transition,
                    out rejectReason)
                : WalkTransferPolicy.TryEvaluateDiceToDice(
                    target,
                    fromLevel,
                    registry,
                    fromSurface,
                    targetSurface,
                    standingDice,
                    isJumping,
                    reach,
                    allowDescentOnly,
                    out transition,
                    out rejectReason);
            return evaluated;
        }

        static string FormatDice(DiceController dice) {
            return dice != null ? dice.name : "(none)";
        }
    }
}
