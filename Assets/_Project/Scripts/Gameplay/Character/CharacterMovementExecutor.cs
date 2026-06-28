using System;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Coupling;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public sealed class CharacterMovementExecutor
    {
        Board board;
        MovementTransitionEvaluator movementTransition;
        CharacterMovementSettings movementSettings;
        CharacterStandingController standing;
        CharacterTransformDriver transformDriver;
        DiceCharacterCoupling coupling;

        public CharacterMovementExecutor(
            Board board,
            MovementTransitionEvaluator movementTransition,
            CharacterMovementSettings movementSettings,
            CharacterStandingController standingController,
            CharacterTransformDriver transformDriver,
            DiceCharacterCoupling diceCharacterCoupling) {
            this.board = board;
            this.movementTransition = movementTransition;
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
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            float halfExtent,
            bool isJumping,
            bool hasJumpCapability,
            JumpCoupledMoveCapability jumpCapability,
            System.Action<string> logJumpParallelRoll,
            DissolveDescentHoldState dissolveHold,
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
                    standing.ApplyFromTransition(plan.Transition, plan.ToCell);
                    return false;

                case CharacterMoveKind.StepToFloor:
                    standing.ApplyFromTransition(MovementTransition.Walkable(null, SurfaceLayer.Floor), plan.ToCell);
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
                        && Mathf.Abs(fromSurfaceY - board.FloorSurfaceWorldY) <= movementSettings.MaxStepHeight) {
                        standing.ApplyFromTransition(
                            MovementTransition.Walkable(null, SurfaceLayer.Floor),
                            plan.ToCell);
                        return false;
                    }

                    nextXZ = transformDriver.ClampToCellInterior(nextXZ, plan.FromCell, halfExtent);
                    return false;

                case CharacterMoveKind.Blocked:
                    if (dissolveHold != null
                        && dissolveHold.TryApplyHold(
                            plan.FromCell,
                            plan.ToCell,
                            fromLayer,
                            fromSurfaceY,
                            move,
                            plan.Direction,
                            standing,
                            movementTransition,
                            movementSettings,
                            standing.ApplyFromTransition)) {
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

        bool TryExecuteCoupledPlan(
            CharacterMovePlan plan,
            Vector2 nextXZ,
            float halfExtent,
            JumpCoupledMoveCapability jumpCapability,
            System.Action<string> logJumpParallelRoll) {
            switch (plan.CoupledIntent) {
                case CoupledMoveIntent.GroundParallelRoll:
                    return coupling.TryBeginGroundParallelRoll(plan.Direction, nextXZ, halfExtent);
                case CoupledMoveIntent.GroundTopFallRoll:
                    return coupling.TryBeginGroundTopFallRoll(plan.Direction, nextXZ, halfExtent);
                case CoupledMoveIntent.JumpTopFallRoll:
                    return coupling.TryBeginJumpTopFallRoll(plan.Direction, nextXZ, halfExtent);
                case CoupledMoveIntent.JumpGridMove:
                    return coupling.TryBeginJumpGridMoveForTransition(
                        plan.FromCell,
                        plan.ToCell,
                        plan.Transition,
                        plan.Direction,
                        nextXZ,
                        halfExtent,
                        jumpCapability,
                        logJumpParallelRoll);
                default:
                    return false;
            }
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
            Vector2Int standingCell,
            Vector2Int nextCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            Vector2 move,
            Direction direction,
            CharacterStandingController standing,
            MovementTransitionEvaluator movementTransition,
            CharacterMovementSettings movementSettings,
            System.Action<MovementTransition, Vector2Int> applyStanding) {
            var standingDice = standing.ResolveStandingDiceForMovement();
            if (standingDice == null || !standingDice.IsDissolving) {
                Reset();
                return false;
            }

            if (!TryGetPrimaryDirection(move, out var moveDir) || moveDir != direction) {
                Reset();
                return false;
            }

            if (!movementTransition.IsDescentBlockedOnlyByStepHeight(
                standingCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standing.Tier)) {
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

            var transition = movementTransition.Evaluate(
                standingCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standing.Tier,
                ignoreStepHeight: true);
            if (transition.Kind != MovementTransitionKind.Walkable) {
                Reset();
                return false;
            }

            applyStanding(transition, nextCell);
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
