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
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

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
                AiSubGoalKind.ReachParticipant => BuildReachAction(goal, subGoal, snapshot, character, settings),
                AiSubGoalKind.ReachWorkDie => BuildReachAction(goal, subGoal, snapshot, character, settings),
                AiSubGoalKind.OrientDie => BuildOrientAction(subGoal, snapshot, character, settings),
                AiSubGoalKind.JoinCluster => BuildJoinClusterAction(subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.LiftDie => BuildLiftAction(subGoal, snapshot, character),
                _ => BuildReachAction(goal, subGoal, snapshot, character, settings)
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
                placeDirection = FindBestPlaceDirection(character);
            }

            if (!placeDirection.HasValue) {
                return null;
            }

            return new Application.Actions.PlaceCarriedDiceAction(placeDirection.Value);
        }

        static Direction? FindBestPlaceDirection(CharacterController character) {
            for (var i = 0; i < Directions.Length; i++) {
                if (character.CanPlaceCarriedAt(Directions[i])) {
                    return Directions[i];
                }
            }

            return null;
        }

        static Application.AiDiscreteAction BuildReachAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings) {
            var targetDie = subGoal.TargetDie;
            if (IsStandingOnTarget(snapshot, targetDie)) {
                subGoal.MarkComplete();
                return null;
            }

            var targetCell = subGoal.TargetCell;
            var avoidClusterCells = subGoal.Kind == AiSubGoalKind.ReachWorkDie
                && goal?.ClusterDice != null
                && targetDie != null
                && !ClusterSelectionEvaluator.ClusterContainsController(goal.ClusterDice, targetDie);

            if (targetDie != null && snapshot.PlayerCell == targetDie.CurrentState.GridPos) {
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

            var constraints = avoidClusterCells
                ? new AiNavigationConstraints(ClusterSelectionEvaluator.GetClusterCells(goal.ClusterDice))
                : AiNavigationConstraints.None;
            var preferJump = subGoal.Kind != AiSubGoalKind.ReachWorkDie;

            return SelectMovementAction(
                targetCell,
                snapshot,
                character,
                settings,
                preferJump,
                standOnDie: targetDie,
                constraints);
        }

        static Application.AiDiscreteAction BuildOrientAction(
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings) {
            var die = subGoal.TargetDie;
            if (die == null) {
                return null;
            }

            if (die.CurrentState.Orientation.Top == subGoal.TargetFace) {
                subGoal.MarkComplete();
                return null;
            }

            if (!IsStandingOnTarget(snapshot, die)) {
                return SelectMovementAction(
                    die.CurrentState.GridPos,
                    snapshot,
                    character,
                    settings,
                    preferJump: false,
                    standOnDie: die,
                    AiNavigationConstraints.None);
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("OrientPlan FAILED navigation-query-unavailable");
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
                subGoal.TargetFace,
                allowJump,
                out var orientStep,
                out var remainingRolls)) {
                Application.AiDebugLog.Log(
                    $"OrientPlan FAILED die={die.name} cell={die.CurrentState.GridPos} " +
                    $"currentTop={die.CurrentState.Orientation.Top} targetTop={subGoal.TargetFace}");
                return null;
            }

            Application.AiDebugLog.Log(
                $"OrientPlan die={die.name} currentTop={die.CurrentState.Orientation.Top} targetTop={subGoal.TargetFace} " +
                $"step={orientStep.Direction} mode={orientStep.Mode} landing={orientStep.LandingCell} " +
                $"landingTier={orientStep.LandingTier} remainingRolls={remainingRolls}");

            return BuildWorkDieRollAction(orientStep, die, settings, allowJump);
        }

        static Application.AiDiscreteAction BuildJoinClusterAction(
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
            if (state.GridPos == subGoal.TargetCell
                && state.Orientation.Top == subGoal.TargetFace) {
                subGoal.MarkComplete();
                return null;
            }

            if (!IsStandingOnTarget(snapshot, die)) {
                return SelectMovementAction(
                    die.CurrentState.GridPos,
                    snapshot,
                    character,
                    settings,
                    preferJump: false,
                    standOnDie: die,
                    AiNavigationConstraints.None);
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("JoinPlan FAILED navigation-query-unavailable");
                return null;
            }

            subGoal.TryAdvanceJoinSlideStep(state);

            if (!subGoal.HasJoinSlidePlan && state.Orientation.Top != subGoal.TargetFace) {
                return BuildOrientAction(subGoal, snapshot, character, settings);
            }

            if (!TryEnsureJoinSlidePlan(subGoal, state)) {
                Application.AiDebugLog.Log(
                    $"JoinPlan FAILED die={die.name} cell={state.GridPos} target={subGoal.TargetCell} " +
                    $"top={state.Orientation.Top}");
                return null;
            }

            var plan = subGoal.JoinSlidePlan.Value;
            var stepIndex = subGoal.JoinSlideStepIndex;
            if (stepIndex >= plan.Directions.Count) {
                if (state.GridPos == subGoal.TargetCell
                    && state.Orientation.Top == subGoal.TargetFace) {
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
                Application.AiDebugLog.Log(
                    $"JoinPlan FAILED die={die.name} cell={state.GridPos} target={subGoal.TargetCell} " +
                    $"top={state.Orientation.Top} step={stepIndex}/{plan.Directions.Count}");
                return null;
            }

            Application.AiDebugLog.Log(
                $"JoinPlan die={die.name} top={state.Orientation.Top} target={subGoal.TargetCell} " +
                $"stepIndex={stepIndex}/{plan.Directions.Count} planned={plan.Directions[stepIndex]} " +
                $"exec={slideStep.Direction} mode={slideStep.Mode} landing={slideStep.LandingCell} " +
                $"landingTier={slideStep.LandingTier}");

            return BuildWorkDieRollAction(slideStep, die, settings, allowJump);
        }

        static bool TryEnsureJoinSlidePlan(AiSubGoal subGoal, DiceState state) {
            if (subGoal.HasJoinSlidePlan) {
                var existing = subGoal.JoinSlidePlan.Value;
                if (WorkDieSlidePlanner.IsPlanStillValid(existing, subGoal.JoinSlideStepIndex, state)) {
                    return existing.Directions != null && existing.Directions.Count > 0;
                }

                subGoal.ClearJoinSlidePlan();
            }

            if (state.Orientation.Top != subGoal.TargetFace) {
                return false;
            }

            if (!WorkDieSlidePlanner.TryBuildSlidePlan(
                state.GridPos,
                subGoal.TargetCell,
                state.Orientation,
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
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character) {
            var die = subGoal.TargetDie;
            if (die == null) {
                return null;
            }

            if (!IsAdjacentToDice(snapshot, die)) {
                return SelectMovementAction(
                    die.CurrentState.GridPos,
                    snapshot,
                    character,
                    null,
                    preferJump: false,
                    standOnDie: null,
                    AiNavigationConstraints.None);
            }

            var faceDirection = GetFacingDirectionTowardDie(snapshot, die);
            if (!faceDirection.HasValue || !character.CanLiftTarget(die)) {
                return null;
            }

            return new Application.Actions.PulseLiftAction(faceDirection.Value);
        }

        static Application.AiDiscreteAction SelectMovementAction(
            Vector2Int targetCell,
            GameStateSnapshot snapshot,
            CharacterController character,
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
                && settings.AllowJump) {
                var jumpDirection = DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, targetCell);
                if (jumpDirection.HasValue) {
                    Application.AiDebugLog.Log(
                        $"SelectCellStep purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                        $"picked=Jump direction={jumpDirection.Value} detail={candidateLog}");

                    return new Application.Actions.JumpThenMoveAction(
                        jumpDirection.Value,
                        targetCell,
                        settings.JumpMoveMaxFrames,
                        standOnDie);
                }
            }

            Application.AiDebugLog.Log(
                $"SelectCellStep FAILED purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                $"detail={candidateLog}");

            return null;
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

        static Direction? GetFacingDirectionTowardDie(GameStateSnapshot snapshot, DiceController die) {
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
