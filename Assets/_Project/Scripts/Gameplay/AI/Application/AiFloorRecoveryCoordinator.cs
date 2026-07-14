using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.AI.Application.Actions;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    public static class AiFloorRecoveryCoordinator
    {
        public static bool TryBuildAction(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            CharacterController character,
            AiPlayerSettings settings,
            out AiDiscreteAction action) {
            action = null;
            if (session == null || snapshot == null || character == null || settings == null) {
                return false;
            }

            AiFloorRecoveryPlanner.AdvancePhase(session, snapshot, registry, settings);

            switch (session.Phase) {
                case AiFloorRecoveryPhase.ReachAlternateWorkDie:
                    return TryBuildReachAlternateAction(session, snapshot, character, settings, out action);
                case AiFloorRecoveryPhase.ApproachNaturalSpawn:
                    return TryBuildApproachSpawnAction(session, snapshot, character, settings, out action);
                case AiFloorRecoveryPhase.MountNaturalSpawn:
                    return TryBuildMountSpawnAction(session, snapshot, character, settings, out action);
                case AiFloorRecoveryPhase.WaitForNaturalSpawn:
                    action = new WaitForNaturalSpawnAction(
                        Mathf.Max(settings.MoveActionMaxFrames, 30),
                        session.SourceTrappedFace ?? 0);
                    return true;
                default:
                    return false;
            }
        }

        static bool TryBuildReachAlternateAction(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            out AiDiscreteAction action) {
            action = null;
            var workDie = session.AlternateWorkDie;
            if (workDie == null) {
                return false;
            }

            if (snapshot.StandingDice == workDie) {
                return false;
            }

            if (AiFloorRecoveryPlanner.IsReadyToMountAlternate(session, snapshot)) {
                var mountDirection = MatchGoalPlanner.GetFacingDirectionTowardDie(snapshot, workDie)
                    ?? DiceBoardAnalyzer.GetPrimaryDirectionToward(
                        snapshot.PlayerCell,
                        workDie.CurrentState.GridPos);
                if (!mountDirection.HasValue) {
                    return false;
                }

                action = new MoveInDirectionAction(
                    mountDirection.Value,
                    settings.MoveActionMaxFrames,
                    workDie.CurrentState.GridPos,
                    MoveActionPurpose.StandOnDie,
                    workDie);
                return true;
            }

            action = MatchGoalPlanner.BuildNavigateToTarget(
                workDie.CurrentState.GridPos,
                snapshot,
                character,
                settings,
                preferJump: settings.AllowJump);
            return action != null;
        }

        static bool TryBuildApproachSpawnAction(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            out AiDiscreteAction action) {
            action = null;
            var spawnDie = session.SpawnDie;
            if (spawnDie == null) {
                return false;
            }

            if (AiFloorRecoveryPlanner.IsReadyToMountNaturalSpawn(session, snapshot)) {
                session.Phase = AiFloorRecoveryPhase.MountNaturalSpawn;
                return TryBuildMountSpawnAction(session, snapshot, character, settings, out action);
            }

            action = MatchGoalPlanner.BuildNavigateToTarget(
                spawnDie.CurrentState.GridPos,
                snapshot,
                character,
                settings,
                preferJump: settings.AllowJump);
            return action != null;
        }

        static bool TryBuildMountSpawnAction(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            out AiDiscreteAction action) {
            action = null;
            var spawnDie = session.SpawnDie;
            if (spawnDie == null) {
                return false;
            }

            if (!AiFloorRecoveryPlanner.IsReadyToMountNaturalSpawn(session, snapshot)) {
                session.Phase = AiFloorRecoveryPhase.ApproachNaturalSpawn;
                return TryBuildApproachSpawnAction(session, snapshot, character, settings, out action);
            }

            if (snapshot.StandingDice == spawnDie) {
                return false;
            }

            var spawnCell = spawnDie.CurrentState.GridPos;
            var direction = MatchGoalPlanner.GetFacingDirectionTowardDie(snapshot, spawnDie)
                ?? DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, spawnCell);
            if (!direction.HasValue) {
                return false;
            }

            action = new MoveInDirectionAction(
                direction.Value,
                settings.MoveActionMaxFrames,
                spawnCell,
                MoveActionPurpose.StandOnDie,
                spawnDie);
            return true;
        }
    }
}
