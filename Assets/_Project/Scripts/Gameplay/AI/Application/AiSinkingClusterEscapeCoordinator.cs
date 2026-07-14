using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.AI.Application.Actions;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    public static class AiSinkingClusterEscapeCoordinator
    {
        public static bool TryBuildDescendAction(
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            out AiDiscreteAction action) {
            action = null;
            if (!AiSinkingClusterEscapePlanner.NeedsDescent(snapshot)) {
                return false;
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                return false;
            }

            var navState = character.GetAiNavigationState();
            if (!AiSinkingClusterEscapePlanner.TrySelectDescentStep(
                passability,
                navState,
                footingWorldY,
                character.PlayerSlot,
                out var direction,
                out var stepCell,
                out var edgeKind)) {
                if (settings.AllowJump && !character.IsJumping) {
                    foreach (var jumpDirection in new[] {
                        Direction.East, Direction.West, Direction.North, Direction.South }) {
                        var landingCell = navState.Cell + jumpDirection.ToGridDelta();
                        action = new JumpThenMoveAction(
                            jumpDirection,
                            landingCell,
                            settings.JumpMoveMaxFrames);
                        return true;
                    }
                }

                return false;
            }

            var maxFrames = Mathf.Max(settings.MoveActionMaxFrames * 3, settings.RollStepMaxFrames);
            if (edgeKind == MovementTransitionKind.BlockedStepOnly) {
                action = new DissolveDescentAction(direction, stepCell, maxFrames);
            } else {
                action = new MoveToAdjacentCellAction(
                    stepCell,
                    stepCell,
                    maxFrames,
                    MoveActionPurpose.NavigateToCell,
                    null,
                    edgeKind);
            }

            return true;
        }
    }
}
