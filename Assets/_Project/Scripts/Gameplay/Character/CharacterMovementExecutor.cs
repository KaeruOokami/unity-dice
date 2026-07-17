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
    public sealed class CharacterMovementExecutor
    {
        Board board;
        CharacterMovementSettings movementSettings;
        CharacterStandingController standing;
        CharacterTransformDriver transformDriver;
        DiceCharacterCoupling coupling;

        public CharacterMovementExecutor(
            Board board,
            CharacterMovementSettings movementSettings,
            CharacterStandingController standingController,
            CharacterTransformDriver transformDriver,
            DiceCharacterCoupling diceCharacterCoupling) {
            this.board = board;
            this.movementSettings = movementSettings;
            standing = standingController;
            this.transformDriver = transformDriver;
            coupling = diceCharacterCoupling;
        }

        public bool TryExecutePlan(
            CharacterMovePlan plan,
            Vector2 currentXZ,
            ref Vector2 nextXZ,
            Vector2 move,
            int fromLevel,
            float fromSurfaceY,
            float halfExtent,
            bool isJumping,
            bool hasJumpCapability,
            JumpCoupledMoveCapability jumpCapability,
            System.Action<string> logJumpParallelRoll,
            DissolveDescentHoldState dissolveHold,
            PendingJumpLandingState pendingJumpLanding,
            out bool consumedMovement) {
            consumedMovement = false;

            switch (plan.Kind) {
                case CharacterMoveKind.FaceSlide:
                    nextXZ = transformDriver.ClampToCellInterior(nextXZ, plan.FromCell, halfExtent);
                    if (plan.BlockFailedJumpGridFallback && isJumping) {
                        logJumpParallelRoll?.Invoke(
                            $"Execute blocked-fallback reason={plan.BlockReason ?? "face-slide"} " +
                            $"from=({plan.FromCell.x},{plan.FromCell.y})");
                    }

                    return false;

                case CharacterMoveKind.Transfer:
                    if (TryDeferJumpLandingTransfer(
                        isJumping,
                        plan.Transition,
                        plan.ToCell,
                        pendingJumpLanding)) {
                        standing.SetTraversalCellWithoutSupportChange(plan.ToCell);
                        return false;
                    }

                    standing.ApplyFromTransition(plan.Transition, plan.ToCell);
                    return false;

                case CharacterMoveKind.StepToFloor:
                    var floorTransition = MovementTransition.Walkable(null, SurfaceHeightLevel.Floor);
                    if (TryDeferJumpLandingTransfer(
                        isJumping,
                        floorTransition,
                        plan.ToCell,
                        pendingJumpLanding)) {
                        standing.SetTraversalCellWithoutSupportChange(plan.ToCell);
                        return false;
                    }

                    standing.ApplyFromTransition(floorTransition, plan.ToCell);
                    return false;

                case CharacterMoveKind.CoupledDiceMove:
                    if (TryExecuteCoupledPlan(
                        plan,
                        nextXZ,
                        halfExtent,
                        jumpCapability,
                        logJumpParallelRoll)) {
                        consumedMovement = true;
                        return true;
                    }

                    if (plan.BlockFailedJumpGridFallback) {
                        logJumpParallelRoll?.Invoke(
                            $"Execute blocked-fallback coupled from=({plan.FromCell.x},{plan.FromCell.y}) " +
                            $"to=({plan.ToCell.x},{plan.ToCell.y})");
                        nextXZ = transformDriver.ClampToCellInterior(nextXZ, plan.FromCell, halfExtent);
                        return false;
                    }

                    if (plan.CoupledIntent == CoupledMoveIntent.GroundParallelRoll
                        && NormalizedHeight.ToNormalized(
                            fromSurfaceY,
                            board.FloorSurfaceWorldY,
                            board.CellSize) <= movementSettings.MaxWalkStep) {
                        standing.ApplyFromTransition(
                            MovementTransition.Walkable(null, SurfaceHeightLevel.Floor),
                            plan.ToCell);
                        return false;
                    }

                    nextXZ = transformDriver.ClampToCellInterior(nextXZ, plan.FromCell, halfExtent);
                    return false;

                case CharacterMoveKind.Blocked:
                    if (dissolveHold != null
                        && dissolveHold.TryApplyHold(
                            plan.ToCell,
                            move,
                            plan.Direction,
                            plan.Transition,
                            movementSettings,
                            standing.ApplyFromTransition)) {
                        nextXZ = transformDriver.ClampToCellInterior(nextXZ, plan.FromCell, halfExtent);
                        return false;
                    }

                    nextXZ = CharacterTransformDriver.CancelMoveIntoDirection(currentXZ, nextXZ, plan.Direction);
                    nextXZ = transformDriver.ClampToCellInterior(nextXZ, plan.FromCell, halfExtent);
                    return false;

                default:
                    nextXZ = transformDriver.ClampToCellInterior(nextXZ, plan.FromCell, halfExtent);
                    return false;
            }
        }

        static bool TryDeferJumpLandingTransfer(
            bool isJumping,
            MovementTransition transition,
            Vector2Int toCell,
            PendingJumpLandingState pendingJumpLanding) {
            return isJumping
                && pendingJumpLanding != null
                && pendingJumpLanding.TryDefer(transition, toCell);
        }

        bool TryExecuteCoupledPlan(
            CharacterMovePlan plan,
            Vector2 nextXZ,
            float halfExtent,
            JumpCoupledMoveCapability jumpCapability,
            System.Action<string> logJumpParallelRoll) {
            if (plan.CoupledIntent == CoupledMoveIntent.GroundIceSlide) {
                return plan.HasDiceSlidePlan
                    && coupling.TryBeginGroundIceSlide(
                        plan.DiceSlidePlan,
                        plan.Direction,
                        plan.Transition.TargetDice,
                        nextXZ,
                        halfExtent);
            }

            if (!plan.HasDiceGridMovePlan) {
                return false;
            }

            switch (plan.CoupledIntent) {
                case CoupledMoveIntent.GroundParallelRoll:
                    return coupling.TryBeginGroundParallelRoll(
                        plan.DiceGridMovePlan,
                        nextXZ,
                        halfExtent);
                case CoupledMoveIntent.GroundTopFallRoll:
                    return coupling.TryBeginGroundTopFallRoll(
                        plan.DiceGridMovePlan,
                        nextXZ,
                        halfExtent);
                case CoupledMoveIntent.JumpTopFallRoll:
                    return coupling.TryBeginJumpTopFallRoll(
                        plan.DiceGridMovePlan,
                        nextXZ,
                        halfExtent);
                case CoupledMoveIntent.JumpGridMove:
                    return coupling.TryBeginJumpGridMoveForTransition(
                        plan.DiceGridMovePlan,
                        nextXZ,
                        halfExtent,
                        logJumpParallelRoll);
                default:
                    return false;
            }
        }
    }

    public sealed class PendingJumpLandingState
    {
        MovementTransition transition;
        Vector2Int toCell;
        bool hasPending;

        public bool HasPending => hasPending;

        public bool TryDefer(MovementTransition transition, Vector2Int toCell) {
            if (transition.Kind != MovementTransitionKind.Walkable) {
                return false;
            }

            this.transition = transition;
            this.toCell = toCell;
            hasPending = true;
            return true;
        }

        public bool TryCommit(Action<MovementTransition, Vector2Int> applyStanding) {
            if (!hasPending) {
                return false;
            }

            applyStanding(transition, toCell);
            Clear();
            return true;
        }

        public void Clear() {
            hasPending = false;
            transition = default;
            toCell = default;
        }
    }

    public sealed class DissolveDescentHoldState
    {
        Direction? holdDirection;
        float holdTime;

        public void Reset() {
            holdDirection = null;
            holdTime = 0f;
        }

        public bool TryApplyHold(
            Vector2Int nextCell,
            Vector2 move,
            Direction direction,
            MovementTransition transition,
            CharacterMovementSettings movementSettings,
            System.Action<MovementTransition, Vector2Int> applyStanding) {
            if (!transition.IsDissolveDescentHold) {
                Reset();
                return false;
            }

            if (!TryGetPrimaryDirection(move, out var moveDir) || moveDir != direction) {
                Reset();
                return false;
            }

            if (holdDirection != direction) {
                holdDirection = direction;
                holdTime = 0f;
            }

            holdTime += Time.deltaTime;
            if (holdTime < movementSettings.DissolveDescentHoldDuration) {
                return true;
            }

            var route = transition.TargetLevel == SurfaceHeightLevel.Floor
                ? MovementTransitionRoute.FloorTransfer
                : MovementTransitionRoute.HeightTransfer;
            applyStanding(
                MovementTransition.Walkable(transition.TargetDice, transition.TargetLevel, route),
                nextCell);
            Reset();
            return true;
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
