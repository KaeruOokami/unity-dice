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

            var dieCell = die.CurrentState.GridPos;
            if (!IsAdjacentToCell(snapshot, dieCell)) {
                return SelectMovementAction(
                    dieCell,
                    snapshot,
                    character,
                    settings,
                    preferJump: false,
                    standOnDie: null,
                    AiNavigationConstraints.None);
            }

            if (DiceBoardAnalyzer.TryGetRollDirectionForTopFace(
                die.CurrentState.Orientation,
                subGoal.TargetFace,
                out var rollDirection)) {
                return new Application.Actions.MoveInDirectionAction(
                    rollDirection,
                    settings.MoveActionMaxFrames,
                    dieCell,
                    Application.Actions.MoveActionPurpose.RollAdjacentDie);
            }

            return null;
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

            if (die.CurrentState.GridPos == subGoal.TargetCell
                && die.CurrentState.Orientation.Top == subGoal.TargetFace) {
                subGoal.MarkComplete();
                return null;
            }

            if (die.CurrentState.GridPos != subGoal.TargetCell
                && DiceBehaviorResolver.GetCapabilities(die.CurrentState.Kind).CanBeLiftedByPlayer
                && CarryPlacementPassability.TryResolveTarget(subGoal.TargetCell, registry, out _, out _)) {
                if (snapshot.PlayerIsCarrying) {
                    return BuildPlaceCarriedAction(AiSubGoal.PlaceCarriedDie(subGoal.TargetCell), character);
                }

                if (!IsAdjacentToDice(snapshot, die)) {
                    return SelectMovementAction(
                        die.CurrentState.GridPos,
                        snapshot,
                        character,
                        settings,
                        preferJump: false,
                        standOnDie: null,
                        AiNavigationConstraints.None);
                }

                var faceDirection = GetFacingDirectionTowardDie(snapshot, die);
                if (faceDirection.HasValue) {
                    return new Application.Actions.LiftSequenceAction(faceDirection.Value, subGoal.TargetCell);
                }
            }

            return BuildOrientAction(
                AiSubGoal.OrientDie(die, subGoal.TargetFace),
                snapshot,
                character,
                settings);
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
