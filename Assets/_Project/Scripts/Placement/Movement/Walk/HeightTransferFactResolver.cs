using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public sealed class HeightTransferFactResolver
    {
        readonly DiceRegistry registry;

        public HeightTransferFactResolver(DiceRegistry registry) {
            this.registry = registry;
        }

        public HeightTransferFacts Resolve(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            bool isJumping,
            bool allowJumpGridMove,
            HeightReachEvaluation reach) {
            var sameTierTarget = standingDice != null
                ? registry.GetTransferTargetAt(standingDice, fromCell, direction, fromLevel)
                : null;

            // Dice axis: effective couple + movable, not static CanGridRoll (stuck Magnet).
            var preferCoupledGridRoll = isJumping
                && allowJumpGridMove
                && standingDice != null
                && standingDice.IsPlayerMovable
                && standingDice.CanJumpCoupleWithPlayer
                && !JumpPlayerTransferPolicy.IsSoloLeaveStandingSurface(standingDice);

            var canSameTier = false;
            MovementTransition sameTierTransition = default;
            string sameTierRejectReason = null;
            if (!preferCoupledGridRoll
                && sameTierTarget != null
                && HeightTransferTargetEvaluator.TryEvaluateToTarget(
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    isJumping,
                    reach,
                    registry,
                    sameTierTarget,
                    dissolveDescentHoldOnly: false,
                    out sameTierTransition,
                    out sameTierRejectReason)) {
                canSameTier = true;
            } else if (preferCoupledGridRoll) {
                sameTierRejectReason = "skipped-for-coupled-grid-roll";
            }

            DiceController lowerLevelTarget = null;
            var lowerLevelTargetLevel = SurfaceHeightLevel.Floor;
            if (TryResolveLowerLevelTargetAt(fromLevel, toCell, out lowerLevelTarget, out lowerLevelTargetLevel)) {
                // resolved
            }

            var hasLowerFallback = lowerLevelTarget != null
                && lowerLevelTarget != sameTierTarget
                && (sameTierTarget == null
                    || HeightTransferActionSelector.IsStepHeightRejectReason(sameTierRejectReason));

            var canLowerLevelWalk = false;
            MovementTransition lowerLevelWalkTransition = default;
            if (hasLowerFallback
                && HeightTransferTargetEvaluator.TryEvaluateToTarget(
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    isJumping,
                    reach,
                    registry,
                    lowerLevelTarget,
                    dissolveDescentHoldOnly: false,
                    out var evaluatedLower,
                    out _)
                && evaluatedLower.Kind == MovementTransitionKind.Walkable) {
                canLowerLevelWalk = true;
                lowerLevelWalkTransition = evaluatedLower;
            }

            var canDissolve = false;
            MovementTransition dissolveTransition = default;
            if (fromSurface.IsErasingStandingSurface
                && !isJumping
                && hasLowerFallback
                && !canLowerLevelWalk
                && HeightTransferTargetEvaluator.TryEvaluateToTarget(
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    isJumping: false,
                    reach,
                    registry,
                    lowerLevelTarget,
                    dissolveDescentHoldOnly: true,
                    out dissolveTransition,
                    out _)) {
                canDissolve = true;
            }

            var canLowerLevelJump = JumpPlayerTransferPolicy.CanUsePlayerOnlyLowerLevelJump(
                isJumping,
                standingDice);

            return new HeightTransferFacts(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                direction,
                isJumping,
                allowJumpGridMove,
                reach,
                sameTierTarget,
                lowerLevelTarget,
                lowerLevelTargetLevel,
                preferCoupledGridRoll,
                canSameTier,
                sameTierTransition,
                sameTierRejectReason,
                canLowerLevelWalk,
                lowerLevelWalkTransition,
                canDissolve,
                dissolveTransition,
                canLowerLevelJump);
        }

        bool TryResolveLowerLevelTargetAt(
            int fromLevel,
            Vector2Int toCell,
            out DiceController targetDice,
            out int targetLevel) {
            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;

            if (JumpPlayerTransferPolicy.IsLowerLevelTransfer(fromLevel, SurfaceHeightLevel.Bottom)
                && registry.TryGetBottomAt(toCell, out var bottom)
                && bottom != null
                && !GhostPlacementRules.IsPlayerPassThrough(bottom)) {
                targetDice = bottom;
                targetLevel = SurfaceHeightLevel.Bottom;
                return true;
            }

            return false;
        }
    }
}
