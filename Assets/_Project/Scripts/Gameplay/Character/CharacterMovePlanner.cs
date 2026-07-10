using System;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Coupling;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public sealed class CharacterMovePlanner
    {
        const float EdgeEpsilon = 0.001f;

        Board board;
        MovementTransitionEvaluator movementTransition;
        CharacterTransformDriver transformDriver;
        Action<string> logJumpParallelRoll;

        public CharacterMovePlanner(
            Board board,
            MovementTransitionEvaluator movementTransition,
            CharacterTransformDriver transformDriver,
            Action<string> logJumpParallelRoll) {
            this.board = board;
            this.movementTransition = movementTransition;
            this.transformDriver = transformDriver;
            this.logJumpParallelRoll = logJumpParallelRoll;
        }

        public CharacterMovePlan TryBuildPlan(
            Vector2 currentXZ,
            Vector2 move,
            Vector2Int standingCell,
            int fromLevel,
            float footingWorldY,
            float rollTriggerHalfExtent,
            CharacterStandingController standing,
            bool isJumping,
            bool hasJumpCapability,
            JumpCoupledMoveCapability jumpCapability,
            PlayerSlot? movementOwner) {
            var allowCrossCell = !isJumping || (hasJumpCapability && jumpCapability.AllowCrossCellMove);
            var allowDiceGridMove = hasJumpCapability && jumpCapability.AllowDiceGridMove;
            var passabilityContext = isJumping
                ? PassabilityContext.Jump(allowDiceGridMove, jumpCapability.AllowTierChange, footingWorldY, movementOwner)
                : PassabilityContext.ForGround(footingWorldY, movementOwner);

            var nextCell = ResolveNextCell(
                standingCell,
                fromLevel,
                currentXZ,
                move,
                rollTriggerHalfExtent,
                standing,
                isJumping,
                hasJumpCapability,
                jumpCapability,
                allowCrossCell,
                allowDiceGridMove,
                passabilityContext);

            if (nextCell == standingCell) {
                if (isJumping
                    && hasJumpCapability
                    && jumpCapability.AllowTierChange
                    && movementTransition.TryEvaluatePlayerOnlyTierDemote(
                        standingCell,
                        fromLevel,
                        standing.ResolveStandingDiceForMovement(),
                        passabilityContext,
                        out var demoteTransition)) {
                    TryGetPrimaryDirection(move, out var demoteDirection);
                    return new CharacterMovePlan {
                        Kind = CharacterMoveKind.Transfer,
                        FromCell = standingCell,
                        ToCell = standingCell,
                        Direction = demoteDirection,
                        Transition = demoteTransition
                    };
                }

                return CharacterMovePlan.FaceSlide(standingCell);
            }

            if (!allowCrossCell) {
                return CharacterMovePlan.FaceSlide(standingCell);
            }

            if (!MovementTransitionEvaluator.IsOrthogonalWithinDistance(
                standingCell,
                nextCell,
                GetMaxMovementCellDistance(standingCell, nextCell, isJumping, hasJumpCapability, jumpCapability))) {
                return CharacterMovePlan.FaceSlide(standingCell);
            }

            if (!MovementTransitionEvaluator.TryGetDirectionBetween(standingCell, nextCell, out var direction)) {
                return CharacterMovePlan.FaceSlide(standingCell);
            }

            var standingDice = standing.ResolveStandingDiceForMovement();
            var cellDistance = MovementTransitionEvaluator.GetOrthogonalDistance(standingCell, nextCell);
            var transition = cellDistance > 1
                ? movementTransition.EvaluateToTargetCell(
                    standingCell,
                    nextCell,
                    fromLevel,
                    standingDice,
                    passabilityContext)
                : movementTransition.Evaluate(
                    standingCell,
                    fromLevel,
                    direction,
                    standingDice,
                    passabilityContext);

            if (isJumping) {
                logJumpParallelRoll?.Invoke(
                    $"Plan jump from=({standingCell.x},{standingCell.y}) " +
                    $"to=({nextCell.x},{nextCell.y}) dir={direction} cellDistance={cellDistance} " +
                    $"transition={transition.Kind} targetLevel={transition.TargetLevel}");
            }

            switch (transition.Kind) {
                case MovementTransitionKind.Walkable:
                    if (transition.Route == MovementTransitionRoute.TopFall
                        && transition.HasDiceGridMovePlan) {
                        return BuildCoupledDiceMovePlan(
                            standingCell,
                            nextCell,
                            direction,
                            transition,
                            isJumping
                                ? CoupledMoveIntent.JumpTopFallRoll
                                : CoupledMoveIntent.GroundTopFallRoll);
                    }

                    if (transition.Route == MovementTransitionRoute.CoupledGridMove
                        && isJumping
                        && transition.HasDiceGridMovePlan) {
                        return BuildCoupledDiceMovePlan(
                            standingCell,
                            nextCell,
                            direction,
                            transition,
                            CoupledMoveIntent.JumpGridMove,
                            blockFailedJumpGridFallback: true);
                    }

                    return new CharacterMovePlan {
                        Kind = CharacterMoveKind.Transfer,
                        FromCell = standingCell,
                        ToCell = nextCell,
                        Direction = direction,
                        Transition = transition
                    };

                case MovementTransitionKind.CanRoll:
                    if (transition.HasDiceGridMovePlan
                        && !isJumping
                        && TryGetPrimaryDirection(move, out var moveDir)
                        && moveDir == direction) {
                        return BuildCoupledDiceMovePlan(
                            standingCell,
                            nextCell,
                            direction,
                            transition,
                            CoupledMoveIntent.GroundParallelRoll);
                    }

                    return CharacterMovePlan.FaceSlide(standingCell);

                case MovementTransitionKind.IceSlide:
                    if (transition.HasDiceSlidePlan
                        && !isJumping
                        && TryGetPrimaryDirection(move, out var iceMoveDir)
                        && iceMoveDir == direction) {
                        return new CharacterMovePlan {
                            Kind = CharacterMoveKind.CoupledDiceMove,
                            FromCell = standingCell,
                            ToCell = transition.DiceSlidePlan.To.GridPos,
                            Direction = direction,
                            Transition = transition,
                            CoupledIntent = CoupledMoveIntent.GroundIceSlide,
                            HasDiceSlidePlan = true,
                            DiceSlidePlan = transition.DiceSlidePlan
                        };
                    }

                    return CharacterMovePlan.FaceSlide(standingCell);

                case MovementTransitionKind.Blocked:
                case MovementTransitionKind.BlockedStepOnly:
                    return new CharacterMovePlan {
                        Kind = CharacterMoveKind.Blocked,
                        FromCell = standingCell,
                        ToCell = nextCell,
                        Direction = direction,
                        Transition = transition
                    };

                default:
                    return CharacterMovePlan.FaceSlide(standingCell);
            }
        }

        static CharacterMovePlan BuildCoupledDiceMovePlan(
            Vector2Int fromCell,
            Vector2Int toCell,
            Direction direction,
            MovementTransition transition,
            CoupledMoveIntent intent,
            bool blockFailedJumpGridFallback = false) {
            return new CharacterMovePlan {
                Kind = CharacterMoveKind.CoupledDiceMove,
                FromCell = fromCell,
                ToCell = toCell,
                Direction = direction,
                Transition = transition,
                CoupledIntent = intent,
                HasDiceGridMovePlan = transition.HasDiceGridMovePlan,
                DiceGridMovePlan = transition.DiceGridMovePlan,
                BlockFailedJumpGridFallback = blockFailedJumpGridFallback
            };
        }

        public int GetMaxMovementCellDistance(
            Vector2Int standingCell,
            Vector2Int nextCell,
            bool isJumping,
            bool hasJumpCapability,
            JumpCoupledMoveCapability jumpCapability) {
            if (!isJumping) {
                return 1;
            }

            var distance = MovementTransitionEvaluator.GetOrthogonalDistance(standingCell, nextCell);
            var resolvedDistance = distance > 0 ? distance : 1;
            if (hasJumpCapability && jumpCapability.AllowCrossCellMove) {
                return resolvedDistance;
            }

            return Mathf.Min(resolvedDistance, 1);
        }

        Vector2Int ResolveNextCell(
            Vector2Int standingCell,
            int fromLevel,
            Vector2 currentXZ,
            Vector2 move,
            float rollTriggerHalfExtent,
            CharacterStandingController standing,
            bool isJumping,
            bool hasJumpCapability,
            JumpCoupledMoveCapability jumpCapability,
            bool allowCrossCell,
            bool allowDiceGridMove,
            PassabilityContext passabilityContext) {
            if (TryGetPrimaryDirection(move, out var moveDir)) {
                if (transformDriver.IsAtOrPastRollTrigger(currentXZ, standingCell, moveDir, rollTriggerHalfExtent)) {
                    if (!allowCrossCell) {
                        return standingCell;
                    }

                    if (allowDiceGridMove
                        && jumpCapability.MaxDistance > 0
                        && TryResolveJumpParallelRollTarget(
                            standingCell,
                            fromLevel,
                            moveDir,
                            jumpCapability.MaxDistance,
                            standing,
                            passabilityContext,
                            out var parallelRollTarget)) {
                        logJumpParallelRoll?.Invoke(
                            $"ResolveNextCell parallel-roll from=({standingCell.x},{standingCell.y}) " +
                            $"to=({parallelRollTarget.x},{parallelRollTarget.y}) dir={moveDir}");
                        return parallelRollTarget;
                    }

                    return standingCell + moveDir.ToGridDelta();
                }

                return standingCell;
            }

            var positionCell = board.WorldToGrid(new Vector3(currentXZ.x, 0f, currentXZ.y));
            if (positionCell == standingCell) {
                return standingCell;
            }

            if (!allowCrossCell) {
                return standingCell;
            }

            if (MovementTransitionEvaluator.IsOrthogonalWithinDistance(standingCell, positionCell, 1)) {
                return positionCell;
            }

            return standingCell;
        }

        bool TryResolveJumpParallelRollTarget(
            Vector2Int standingCell,
            int fromLevel,
            Direction direction,
            int maxRollDistance,
            CharacterStandingController standing,
            PassabilityContext passabilityContext,
            out Vector2Int rollTarget) {
            rollTarget = standingCell;
            if (standing.CurrentDice == null || maxRollDistance < 1) {
                return false;
            }

            var standingDice = standing.ResolveStandingDiceForMovement();
            for (var distance = maxRollDistance; distance >= 1; distance--) {
                if (movementTransition.TryGetJumpParallelRollTarget(
                    standingCell,
                    direction,
                    standingDice,
                    fromLevel,
                    distance,
                    passabilityContext,
                    out var candidate,
                    out _)) {
                    rollTarget = candidate;
                    return true;
                }
            }

            return false;
        }

        static bool TryGetPrimaryDirection(Vector2 move, out Direction direction) {
            direction = Direction.North;
            if (move.sqrMagnitude <= 0f) {
                return false;
            }

            if (Mathf.Abs(move.x) >= Mathf.Abs(move.y)) {
                direction = move.x >= 0f ? Direction.East : Direction.West;
            } else {
                direction = move.y >= 0f ? Direction.North : Direction.South;
            }

            return true;
        }
    }
}
