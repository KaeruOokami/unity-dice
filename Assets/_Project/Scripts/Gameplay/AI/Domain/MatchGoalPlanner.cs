using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Character;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class MatchGoalPlanner
    {
        public static Application.AiDiscreteAction BuildAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (goal == null || subGoal == null || character == null) {
                return null;
            }

            if (snapshot.PlayerIsCarrying || subGoal.Kind == AiSubGoalKind.PlaceCarriedDie) {
                return BuildPlaceCarriedAction(subGoal, character);
            }

            return subGoal.Kind switch {
                AiSubGoalKind.ReachParticipant => BuildReachAction(goal, subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.ReachWorkDie => BuildReachAction(goal, subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.OrientDie => BuildOrientAction(subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.JoinCluster => BuildJoinClusterAction(goal, subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.LiftDie => BuildLiftAction(goal, subGoal, snapshot, character, registry, settings),
                _ => BuildReachAction(goal, subGoal, snapshot, character, registry, settings)
            };
        }

        static Application.AiDiscreteAction BuildPlaceCarriedAction(AiSubGoal subGoal, CharacterController character) {
            var origin = character.StandingGridCell;
            var delta = subGoal.TargetCell - origin;

            Direction? placeDirection = null;
            if (delta.x == 1 && delta.y == 0) {
                placeDirection = Direction.East;
            } else if (delta.x == -1 && delta.y == 0) {
                placeDirection = Direction.West;
            } else if (delta.x == 0 && delta.y == 1) {
                placeDirection = Direction.North;
            } else if (delta.x == 0 && delta.y == -1) {
                placeDirection = Direction.South;
            }

            if (!placeDirection.HasValue || !character.CanPlaceCarriedAt(placeDirection.Value)) {
                Application.AiDebugLog.Log(
                    $"PlaceCarried FAILED origin={origin} target={subGoal.TargetCell} " +
                    $"dir={(placeDirection.HasValue ? placeDirection.Value.ToString() : "none")}");
                return null;
            }

            return new Application.Actions.PlaceCarriedDiceAction(placeDirection.Value);
        }

        static Application.AiDiscreteAction BuildReachAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var targetDie = subGoal.TargetDie;
            if (IsStandingOnTarget(snapshot, targetDie)) {
                subGoal.MarkComplete();
                return null;
            }

            if (targetDie == null) {
                return null;
            }

            // Always use live die cell; stale SubGoal.TargetCell alone conflates Top/Bottom stacks.
            var targetCell = targetDie.CurrentState.GridPos;
            var avoidClusterCells = subGoal.Kind == AiSubGoalKind.ReachWorkDie
                && goal?.ClusterDice != null
                && !ClusterSelectionEvaluator.ClusterContainsController(goal.ClusterDice, targetDie);

            // Same grid as the die but standing on another stack tier (e.g. Top over Bottom target).
            if (snapshot.PlayerCell == targetCell
                && snapshot.StandingDice != null
                && snapshot.StandingDice != targetDie) {
                Application.AiDebugLog.Log(
                    $"ReachSameCellWrongTier player={snapshot.PlayerCell} " +
                    $"standOn={snapshot.StandingDice.name} " +
                    $"target={targetDie.name} tier={targetDie.CurrentState.Tier}");
                if (TryBuildLeaveCellAction(snapshot, character, settings, targetCell, out var leaveAction)) {
                    return leaveAction;
                }

                return null;
            }

            if (snapshot.PlayerCell == targetCell) {
                var inCellDirection = GetFacingDirectionTowardDie(snapshot, targetDie)
                    ?? DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, targetCell);
                if (inCellDirection.HasValue) {
                    var maxFrames = settings != null ? settings.MoveActionMaxFrames : 30;
                    Application.AiDebugLog.Log(
                        $"SelectInCellStandOn player={snapshot.PlayerCell} die={targetDie.name} dir={inCellDirection.Value}");

                    return new Application.Actions.MoveInDirectionAction(
                        inCellDirection.Value,
                        maxFrames,
                        targetCell,
                        Application.Actions.MoveActionPurpose.StandOnDie,
                        targetDie);
                }
            }

            var strandedOnProtectedCluster = avoidClusterCells
                && goal.ClusterDice != null
                && ClusterSelectionEvaluator.IsStandingOnCluster(snapshot, goal.ClusterDice)
                && snapshot.StandingDice != null
                && !snapshot.StandingDice.IsSinkErasing
                && !AiFloorRecoveryPlanner.HasAdjacentClusterExternalDie(
                    goal.ClusterDice,
                    goal.Face,
                    snapshot.PlanningDice);

            var constraints = avoidClusterCells && !strandedOnProtectedCluster
                ? AiNavigationConstraints.ForClusterProtection(goal.ClusterDice)
                : AiNavigationConstraints.None;
            if (strandedOnProtectedCluster) {
                Application.AiDebugLog.Log(
                    $"ReachWorkDie allow-cluster-roll player={snapshot.PlayerCell} " +
                    $"standOn={(snapshot.StandingDice != null ? snapshot.StandingDice.name : "none")} " +
                    $"goal={targetCell}");
            }

            return BuildStandOnDieMovement(
                targetDie,
                snapshot,
                character,
                registry,
                settings,
                constraints);
        }

        /// <summary>
        /// Climbing onto a higher-tier die while standing on another die cannot be done by Jump.
        /// Leave the current die first; if leave is impossible, fail planning instead of Jump-looping.
        /// </summary>
        static Application.AiDiscreteAction BuildStandOnDieMovement(
            DiceController targetDie,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings,
            AiNavigationConstraints constraints) {
            if (targetDie == null || snapshot == null) {
                return null;
            }

            var standingOnOtherDie = snapshot.StandingDice != null
                && snapshot.StandingDice != targetDie;
            var needsClimb = RequiresJumpToStandOn(snapshot, targetDie);

            if (needsClimb && standingOnOtherDie) {
                if (TryBuildLeaveCellAction(
                    snapshot,
                    character,
                    settings,
                    snapshot.PlayerCell,
                    out var leaveAction)) {
                    Application.AiDebugLog.Log(
                        $"ReachClimbLeave from={snapshot.PlayerCell} " +
                        $"standOn={snapshot.StandingDice.name} target={targetDie.name} " +
                        $"targetTier={targetDie.CurrentState.Tier}");
                    return leaveAction;
                }

                Application.AiDebugLog.Log(
                    $"ReachClimbBlocked from={snapshot.PlayerCell} " +
                    $"standOn={snapshot.StandingDice.name} target={targetDie.name} " +
                    $"targetTier={targetDie.CurrentState.Tier} reason=no-leave");
                return null;
            }

            var preferJump = settings == null || settings.AllowJump;

            return SelectMovementAction(
                targetDie.CurrentState.GridPos,
                snapshot,
                character,
                registry,
                settings,
                preferJump,
                standOnDie: targetDie,
                constraints);
        }

        static bool RequiresJumpToStandOn(GameStateSnapshot snapshot, DiceController targetDie) {
            if (snapshot == null || targetDie == null) {
                return false;
            }

            var targetLevel = SurfaceHeightLevel.FromDiceStackTier(targetDie.CurrentState.Tier);
            var standingLevel = snapshot.StandingDice != null
                ? SurfaceHeightLevel.FromDiceStackTier(snapshot.StandingDice.CurrentState.Tier)
                : SurfaceHeightLevel.Floor;
            return targetLevel > standingLevel;
        }

        static bool TryBuildLeaveCellAction(
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            Vector2Int blockedCell,
            out Application.AiDiscreteAction action) {
            action = null;
            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                return false;
            }

            var navState = character.GetAiNavigationState();
            var maxSearchSteps = settings != null ? settings.PathSearchMaxSteps : 64;
            AdjacentCellCandidate? bestStep = null;
            var bestScore = float.MinValue;

            foreach (var neighbor in DiceBoardAnalyzer.GetAdjacentCells(blockedCell)) {
                if (!snapshot.IsInPlayerRegion(neighbor)) {
                    continue;
                }

                if (!AiCellMoveEvaluator.TrySelectBestAdjacentCell(
                    passability,
                    navState,
                    neighbor,
                    footingWorldY,
                    character.PlayerSlot,
                    standOnDie: null,
                    maxSearchSteps,
                    AiNavigationConstraints.None,
                    out var step,
                    out _)) {
                    continue;
                }

                // Prefer leaving onto a different cell; higher scores from evaluator win ties.
                if (step.Cell == blockedCell) {
                    continue;
                }

                if (step.Score > bestScore) {
                    bestScore = step.Score;
                    bestStep = step;
                }
            }

            if (!bestStep.HasValue) {
                return false;
            }

            var picked = bestStep.Value;
            var maxFrames = settings != null ? settings.MoveActionMaxFrames : 30;
            Application.AiDebugLog.Log(
                $"LeaveOccupiedStackCell from={blockedCell} step={picked.Cell} edge={picked.EdgeKind}");
            action = new Application.Actions.MoveToAdjacentCellAction(
                picked.Cell,
                picked.Cell,
                maxFrames,
                Application.Actions.MoveActionPurpose.NavigateToCell,
                null,
                picked.EdgeKind);
            return true;
        }

        static Application.AiDiscreteAction BuildOrientAction(
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var die = subGoal.TargetDie;
            if (die == null) {
                return null;
            }

            var state = die.CurrentState;
            if (state.Orientation.Top == subGoal.TargetFace) {
                subGoal.MarkComplete();
                return null;
            }

            if (!IsStandingOnTarget(snapshot, die)) {
                return BuildStandOnDieMovement(
                    die,
                    snapshot,
                    character,
                    registry,
                    settings,
                    AiNavigationConstraints.None);
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("OrientPlan FAILED navigation-query-unavailable");
                return null;
            }

            subGoal.TryAdvanceOrientRollStep(state);

            if (!TryEnsureOrientRollPlan(subGoal, state, passability, character, footingWorldY, settings)) {
                Application.AiDebugLog.Log(
                    $"OrientPlan FAILED die={die.name} cell={state.GridPos} " +
                    $"currentTop={state.Orientation.Top} targetTop={subGoal.TargetFace}");
                return null;
            }

            var plan = subGoal.OrientRollPlan.Value;
            var stepIndex = subGoal.OrientRollStepIndex;
            if (stepIndex >= plan.Directions.Count) {
                if (state.Orientation.Top == subGoal.TargetFace) {
                    subGoal.MarkComplete();
                }

                return null;
            }

            var fromLevel = character.StandingPlacement.Level;
            var allowJump = settings != null && settings.AllowJump;
            if (!WorkDieOrientPlanner.TrySelectNextStep(
                passability,
                die,
                fromLevel,
                footingWorldY,
                character.PlayerSlot,
                plan,
                stepIndex,
                allowJump,
                out var orientStep,
                out var remainingRolls)) {
                subGoal.ClearOrientRollPlan();
                Application.AiDebugLog.Log(
                    $"OrientPlan FAILED die={die.name} cell={state.GridPos} " +
                    $"currentTop={state.Orientation.Top} targetTop={subGoal.TargetFace} " +
                    $"step={stepIndex}/{plan.Directions.Count}");
                return null;
            }

            Application.AiDebugLog.Log(
                $"OrientPlan die={die.name} currentTop={state.Orientation.Top} targetTop={subGoal.TargetFace} " +
                $"stepIndex={stepIndex}/{plan.Directions.Count} planned={plan.Directions[stepIndex]} " +
                $"exec={orientStep.Direction} mode={orientStep.Mode} landing={orientStep.LandingCell} " +
                $"landingTier={orientStep.LandingTier} remainingRolls={remainingRolls}");

            return BuildWorkDieRollAction(orientStep, die, settings, allowJump);
        }

        static bool TryEnsureOrientRollPlan(
            AiSubGoal subGoal,
            DiceState state,
            MovementTransitionEvaluator passability,
            CharacterController character,
            float footingWorldY,
            AiPlayerSettings settings) {
            if (subGoal.HasOrientRollPlan) {
                var existing = subGoal.OrientRollPlan.Value;
                if (WorkDieSlidePlanner.IsPlanStillValid(existing, subGoal.OrientRollStepIndex, state)) {
                    return existing.Directions != null && existing.Directions.Count > 0;
                }

                subGoal.ClearOrientRollPlan();
            }

            var allowJump = settings != null && settings.AllowJump;
            if (!WorkDieOrientPlanner.TryBuildOrientPlan(
                passability,
                subGoal.TargetDie,
                character.StandingPlacement.Level,
                footingWorldY,
                character.PlayerSlot,
                state,
                subGoal.TargetFace,
                allowJump,
                out var plan)
                || plan.Directions == null
                || plan.Directions.Count == 0) {
                return false;
            }

            subGoal.SetOrientRollPlan(plan);
            return true;
        }

        static Application.AiDiscreteAction BuildJoinClusterAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var die = subGoal.TargetDie;
            if (die == null) {
                return null;
            }

            var state = die.CurrentState;
            if (WorkDieSlidePlanner.IsJoinComplete(
                state,
                subGoal.TargetCell,
                subGoal.TargetTier,
                subGoal.TargetFace)) {
                subGoal.MarkComplete();
                return null;
            }

            if (!EnsureJoinTargetAvailable(goal, subGoal, snapshot, registry)) {
                Application.AiDebugLog.Log(
                    $"JoinTarget UNAVAILABLE die={die.name} cell={state.GridPos} " +
                    $"target={subGoal.TargetCell}/{subGoal.TargetTier}");
                return null;
            }

            if (WorkDieSlidePlanner.IsJoinComplete(
                state,
                subGoal.TargetCell,
                subGoal.TargetTier,
                subGoal.TargetFace)) {
                subGoal.MarkComplete();
                return null;
            }

            if (!IsStandingOnTarget(snapshot, die)) {
                if (snapshot.PlayerCell == die.CurrentState.GridPos
                    && snapshot.StandingDice != null
                    && snapshot.StandingDice != die) {
                    Application.AiDebugLog.Log(
                        $"JoinReachSameCellWrongTier player={snapshot.PlayerCell} " +
                        $"standOn={snapshot.StandingDice.name} target={die.name}");
                    if (TryBuildLeaveCellAction(
                        snapshot,
                        character,
                        settings,
                        die.CurrentState.GridPos,
                        out var leaveAction)) {
                        return leaveAction;
                    }

                    return null;
                }

                return BuildStandOnDieMovement(
                    die,
                    snapshot,
                    character,
                    registry,
                    settings,
                    AiNavigationConstraints.None);
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("JoinPlan FAILED navigation-query-unavailable");
                return null;
            }

            subGoal.TryAdvanceJoinSlideStep(state);

            if (!subGoal.HasJoinSlidePlan && state.Orientation.Top != subGoal.TargetFace) {
                return BuildOrientAction(subGoal, snapshot, character, registry, settings);
            }

            if (!TryEnsureJoinSlidePlan(subGoal, state, passability, character, footingWorldY, settings)) {
                // Landing blocked or pathless — retarget once then rebuild.
                if (TryRetargetJoin(goal, subGoal, snapshot, registry)
                    && TryEnsureJoinSlidePlan(subGoal, die.CurrentState, passability, character, footingWorldY, settings)) {
                    Application.AiDebugLog.Log(
                        $"JoinRetargetOk die={die.name} target={subGoal.TargetCell}/{subGoal.TargetTier}");
                } else {
                    Application.AiDebugLog.Log(
                        $"JoinPlan FAILED die={die.name} cell={state.GridPos} " +
                        $"target={subGoal.TargetCell}/{subGoal.TargetTier} top={state.Orientation.Top}");
                    return null;
                }
            }

            var plan = subGoal.JoinSlidePlan.Value;
            var stepIndex = subGoal.JoinSlideStepIndex;
            if (stepIndex >= plan.Directions.Count) {
                state = die.CurrentState;
                if (WorkDieSlidePlanner.IsJoinComplete(
                    state,
                    subGoal.TargetCell,
                    subGoal.TargetTier,
                    subGoal.TargetFace)) {
                    subGoal.MarkComplete();
                }

                return null;
            }

            var fromLevel = character.StandingPlacement.Level;
            var allowJump = settings != null && settings.AllowJump;
            if (!WorkDieSlidePlanner.TrySelectNextStep(
                passability,
                die,
                fromLevel,
                footingWorldY,
                character.PlayerSlot,
                plan,
                stepIndex,
                allowJump,
                out var slideStep)) {
                subGoal.ClearJoinSlidePlan();
                if (TryRetargetJoin(goal, subGoal, snapshot, registry)) {
                    Application.AiDebugLog.Log(
                        $"JoinStepRetarget die={die.name} target={subGoal.TargetCell}/{subGoal.TargetTier}");
                    return null;
                }

                Application.AiDebugLog.Log(
                    $"JoinPlan FAILED die={die.name} cell={state.GridPos} " +
                    $"target={subGoal.TargetCell}/{subGoal.TargetTier} top={state.Orientation.Top} " +
                    $"step={stepIndex}/{plan.Directions.Count}");
                return null;
            }

            Application.AiDebugLog.Log(
                $"JoinPlan die={die.name} top={state.Orientation.Top} " +
                $"target={subGoal.TargetCell}/{subGoal.TargetTier} " +
                $"stepIndex={stepIndex}/{plan.Directions.Count} planned={plan.Directions[stepIndex]} " +
                $"exec={slideStep.Direction} mode={slideStep.Mode} landing={slideStep.LandingCell} " +
                $"landingTier={slideStep.LandingTier}");

            return BuildWorkDieRollAction(slideStep, die, settings, allowJump);
        }

        static bool EnsureJoinTargetAvailable(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            DiceRegistry registry) {
            if (WorkDieSlidePlanner.IsJoinLandingAvailable(
                registry,
                subGoal.TargetDie,
                subGoal.TargetCell,
                subGoal.TargetTier)) {
                return true;
            }

            return TryRetargetJoin(goal, subGoal, snapshot, registry);
        }

        static bool TryRetargetJoin(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            DiceRegistry registry) {
            if (goal?.ClusterDice == null || subGoal.TargetDie == null || snapshot == null) {
                return false;
            }

            var workDieSnapshot = new DiceSnapshot(subGoal.TargetDie);
            if (!WorkDieSlidePlanner.TrySelectJoinTargetCell(
                goal.ClusterDice,
                workDieSnapshot,
                snapshot.PlanningDice,
                registry,
                snapshot.VersusLayout,
                snapshot.PlayerSlot,
                out var cell,
                out var tier,
                excludeCell: subGoal.TargetCell,
                excludeTier: subGoal.TargetTier)) {
                // No exclude: maybe only one valid slot appeared after the blocked one cleared.
                if (!WorkDieSlidePlanner.TrySelectJoinTargetCell(
                    goal.ClusterDice,
                    workDieSnapshot,
                    snapshot.PlanningDice,
                    registry,
                    snapshot.VersusLayout,
                    snapshot.PlayerSlot,
                    out cell,
                    out tier)) {
                    return false;
                }
            }

            if (cell == subGoal.TargetCell && tier == subGoal.TargetTier) {
                return WorkDieSlidePlanner.IsJoinLandingAvailable(
                    registry,
                    subGoal.TargetDie,
                    cell,
                    tier);
            }

            Application.AiDebugLog.Log(
                $"JoinRetarget from={subGoal.TargetCell}/{subGoal.TargetTier} to={cell}/{tier}");
            subGoal.RetargetJoin(cell, tier);
            return true;
        }

        static bool TryEnsureJoinSlidePlan(
            AiSubGoal subGoal,
            DiceState state,
            MovementTransitionEvaluator passability,
            CharacterController character,
            float footingWorldY,
            AiPlayerSettings settings) {
            if (subGoal.HasJoinSlidePlan) {
                var existing = subGoal.JoinSlidePlan.Value;
                if (WorkDieSlidePlanner.IsPlanStillValid(existing, subGoal.JoinSlideStepIndex, state)) {
                    return existing.Directions != null;
                }

                subGoal.ClearJoinSlidePlan();
            }

            if (state.Orientation.Top != subGoal.TargetFace) {
                return false;
            }

            if (WorkDieSlidePlanner.IsJoinComplete(
                state,
                subGoal.TargetCell,
                subGoal.TargetTier,
                subGoal.TargetFace)) {
                subGoal.SetJoinSlidePlan(
                    new WorkDieSlidePlan(state.GridPos, state.Orientation, new List<Direction>()));
                return true;
            }

            var allowJump = settings != null && settings.AllowJump;
            if (!WorkDieSlidePlanner.TryBuildSlidePlan(
                passability,
                subGoal.TargetDie,
                character.StandingPlacement.Level,
                footingWorldY,
                character.PlayerSlot,
                state,
                subGoal.TargetCell,
                subGoal.TargetTier,
                allowJump,
                out var plan)
                || plan.Directions == null
                || plan.Directions.Count == 0) {
                return false;
            }

            subGoal.SetJoinSlidePlan(plan);
            return true;
        }

        static Application.AiDiscreteAction BuildWorkDieRollAction(
            WorkDieRollStep rollStep,
            DiceController die,
            AiPlayerSettings settings,
            bool allowJump) {
            var maxFrames = settings != null ? settings.MoveActionMaxFrames : 30;
            if (rollStep.Mode == WorkDieRollExecutionMode.GroundRoll) {
                return new Application.Actions.MoveToAdjacentCellAction(
                    rollStep.LandingCell,
                    rollStep.LandingCell,
                    maxFrames,
                    Application.Actions.MoveActionPurpose.RollWorkDie,
                    die,
                    rollStep.EdgeKind);
            }

            if (!allowJump) {
                return null;
            }

            return new Application.Actions.JumpThenMoveAction(
                rollStep.Direction,
                rollStep.LandingCell,
                settings != null ? settings.JumpMoveMaxFrames : 48,
                die,
                releaseInputDuringRoll: false,
                expectedLandingTier: rollStep.LandingTier);
        }

        static Application.AiDiscreteAction BuildLiftAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var die = subGoal.TargetDie;
            if (die == null) {
                return null;
            }

            if (snapshot.StandingDice == die) {
                if (TryBuildLeaveCellAction(
                    snapshot,
                    character,
                    settings,
                    snapshot.PlayerCell,
                    out var leaveAction)) {
                    Application.AiDebugLog.Log(
                        $"LiftLeaveStandingDie die={die.name} from={snapshot.PlayerCell}");
                    return leaveAction;
                }

                Application.AiDebugLog.Log(
                    $"LiftLeaveStandingDie FAILED die={die.name} from={snapshot.PlayerCell}");
                return null;
            }

            Vector2Int? placeCell = null;
            if (TryGetFollowingPlaceCell(goal, subGoal, out var plannedPlace)) {
                placeCell = plannedPlace;
            }

            if (placeCell.HasValue
                && LiftJoinPlanner.TrySelectStanceCell(
                    snapshot,
                    registry,
                    die,
                    die.CurrentState.GridPos,
                    placeCell.Value,
                    out var stanceCell)) {
                var needsReposition = snapshot.PlayerCell != stanceCell
                    || (IsAdjacentToDice(snapshot, die)
                        && DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, placeCell.Value) != 1);

                if (needsReposition) {
                    Application.AiDebugLog.Log(
                        $"LiftApproachStance die={die.name} stance={stanceCell} place={placeCell.Value}");
                    return SelectMovementAction(
                        stanceCell,
                        snapshot,
                        character,
                        registry,
                        settings,
                        preferJump: false,
                        standOnDie: null,
                        AiNavigationConstraints.None);
                }
            } else if (!IsAdjacentToDice(snapshot, die)) {
                return SelectMovementAction(
                    die.CurrentState.GridPos,
                    snapshot,
                    character,
                    registry,
                    settings,
                    preferJump: false,
                    standOnDie: null,
                    AiNavigationConstraints.None);
            }

            if (!IsAdjacentToDice(snapshot, die)) {
                return null;
            }

            if (placeCell.HasValue
                && DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, placeCell.Value) != 1) {
                Application.AiDebugLog.Log(
                    $"LiftStanceMismatch die={die.name} player={snapshot.PlayerCell} place={placeCell.Value}");
                return null;
            }

            var faceDirection = GetFacingDirectionTowardDie(snapshot, die);
            if (!faceDirection.HasValue || !character.CanLiftTarget(die)) {
                Application.AiDebugLog.Log(
                    $"Lift FAILED die={die.name} player={snapshot.PlayerCell} " +
                    $"canLift={character.CanLiftTarget(die)}");
                return null;
            }

            return new Application.Actions.PulseLiftAction(faceDirection.Value);
        }

        static bool TryGetFollowingPlaceCell(MatchGoal goal, AiSubGoal liftSubGoal, out Vector2Int placeCell) {
            placeCell = default;
            if (goal?.SubGoals == null || liftSubGoal == null) {
                return false;
            }

            var foundLift = false;
            for (var i = 0; i < goal.SubGoals.Count; i++) {
                var candidate = goal.SubGoals[i];
                if (!foundLift) {
                    if (candidate == liftSubGoal) {
                        foundLift = true;
                    }

                    continue;
                }

                if (candidate.IsComplete) {
                    continue;
                }

                if (candidate.Kind == AiSubGoalKind.PlaceCarriedDie) {
                    placeCell = candidate.TargetCell;
                    return true;
                }

                break;
            }

            return false;
        }

        public static Application.AiDiscreteAction BuildNavigateToTarget(
            Vector2Int targetCell,
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            DiceController standOnDie = null,
            bool preferJump = true,
            DiceRegistry registry = null) {
            return SelectMovementAction(
                targetCell,
                snapshot,
                character,
                registry,
                settings,
                preferJump,
                standOnDie,
                AiNavigationConstraints.None);
        }

        static Application.AiDiscreteAction SelectMovementAction(
            Vector2Int targetCell,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings,
            bool preferJump,
            DiceController standOnDie,
            AiNavigationConstraints constraints) {
            var purpose = standOnDie != null
                ? Application.Actions.MoveActionPurpose.StandOnDie
                : Application.Actions.MoveActionPurpose.NavigateToCell;

            if (standOnDie == null && snapshot.PlayerCell == targetCell) {
                return null;
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("SelectCellStep FAILED navigation-query-unavailable");
                return null;
            }

            var maxSearchSteps = settings != null ? settings.PathSearchMaxSteps : 64;
            var navState = character.GetAiNavigationState();

            if (AiCellMoveEvaluator.TrySelectBestAdjacentCell(
                passability,
                navState,
                targetCell,
                footingWorldY,
                character.PlayerSlot,
                standOnDie,
                maxSearchSteps,
                constraints,
                out var best,
                out var candidateLog)) {
                var maxFrames = character.IsJumping
                    ? (settings != null ? settings.JumpMoveMaxFrames : 45)
                    : (settings != null ? settings.MoveActionMaxFrames : 30);

                Application.AiDebugLog.Log(
                    $"SelectCellStep purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                    $"standOn={(standOnDie != null ? standOnDie.name : "none")} " +
                    $"picked={best.Cell} dir={best.Direction} edge={best.EdgeKind} pathLen={best.PathLength} score={best.Score:F1} " +
                    $"detail={candidateLog}");

                return new Application.Actions.MoveToAdjacentCellAction(
                    best.Cell,
                    targetCell,
                    maxFrames,
                    purpose,
                    standOnDie,
                    best.EdgeKind);
            }

            if (preferJump
                && !character.IsJumping
                && settings != null
                && settings.AllowJump
                && !IsUnsafeClimbJump(snapshot, standOnDie)) {
                var jumpDirection = DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, targetCell);
                if (jumpDirection.HasValue) {
                    Application.AiDebugLog.Log(
                        $"SelectCellStep purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                        $"picked=Jump direction={jumpDirection.Value} detail={candidateLog}");

                    return new Application.Actions.JumpThenMoveAction(
                        jumpDirection.Value,
                        targetCell,
                        settings.JumpMoveMaxFrames,
                        standOnDie,
                        releaseInputDuringRoll: false,
                        expectedLandingTier: standOnDie != null
                            ? standOnDie.CurrentState.Tier
                            : (DiceStackTier?)null);
                }
            }

            if (registry != null
                && LiftClearancePlanner.TryPlan(snapshot, registry, targetCell, out var clearance)) {
                Application.AiDebugLog.Log(
                    $"LiftClearance die={clearance.BlockDie.name} place={clearance.PlaceCell} " +
                    $"dir={clearance.FaceDirection} goal={targetCell}");
                return new Application.Actions.LiftSequenceAction(
                    clearance.FaceDirection,
                    clearance.PlaceCell);
            }

            Application.AiDebugLog.Log(
                $"SelectCellStep FAILED purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                $"detail={candidateLog}");

            return null;
        }

        /// <summary>
        /// Jumping from another die onto a higher-tier target does not remount; leave first instead.
        /// </summary>
        static bool IsUnsafeClimbJump(GameStateSnapshot snapshot, DiceController standOnDie) {
            if (snapshot?.StandingDice == null || standOnDie == null) {
                return false;
            }

            if (snapshot.StandingDice == standOnDie) {
                return false;
            }

            return RequiresJumpToStandOn(snapshot, standOnDie);
        }

        // Legacy scoring removed — cell steps use AiCellMoveEvaluator.

        static bool IsStandingOnTarget(GameStateSnapshot snapshot, DiceController die) {
            return die != null && snapshot.StandingDice == die;
        }

        static bool IsAdjacentToDice(GameStateSnapshot snapshot, DiceController die) {
            if (die == null) {
                return false;
            }

            return IsAdjacentToCell(snapshot, die.CurrentState.GridPos);
        }

        static bool IsAdjacentToCell(GameStateSnapshot snapshot, Vector2Int cell) {
            return DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, cell) == 1;
        }

        public static Direction? GetFacingDirectionTowardDie(GameStateSnapshot snapshot, DiceController die) {
            var delta = die.CurrentState.GridPos - snapshot.PlayerCell;
            if (delta.x == 1 && delta.y == 0) {
                return Direction.East;
            }

            if (delta.x == -1 && delta.y == 0) {
                return Direction.West;
            }

            if (delta.x == 0 && delta.y == 1) {
                return Direction.North;
            }

            if (delta.x == 0 && delta.y == -1) {
                return Direction.South;
            }

            return DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, die.CurrentState.GridPos);
        }
    }
}
