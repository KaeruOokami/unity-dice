using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public enum AiFloorRecoveryPhase
    {
        ReachAlternateWorkDie,
        ApproachNaturalSpawn,
        MountNaturalSpawn,
        WaitForNaturalSpawn
    }

    public sealed class AiFloorRecoverySession
    {
        public AiFloorRecoveryPhase Phase { get; set; }
        public int? SourceTrappedFace { get; set; }
        public DiceController AlternateWorkDie { get; set; }
        public DiceController SpawnDie { get; set; }
    }

    public static class AiFloorRecoveryPlanner
    {
        public static bool NeedsRecovery(GameStateSnapshot snapshot) {
            return snapshot != null
                && snapshot.PlayerIsOnFloor
                && snapshot.StandingDice == null;
        }

        public static AiFloorRecoverySession CreateSession(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings,
            int? sourceTrappedFace) {
            var session = new AiFloorRecoverySession {
                SourceTrappedFace = sourceTrappedFace
            };
            EnsureSessionTargets(session, snapshot, registry, settings);
            session.Phase = ResolveInitialPhase(session, snapshot);
            return session;
        }

        public static void EnsureSessionTargets(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (session == null || snapshot == null || settings == null) {
                return;
            }

            var trappedFace = session.SourceTrappedFace ?? 0;

            if (session.AlternateWorkDie == null
                && TrySelectAlternateSinkingTarget(
                    snapshot,
                    trappedFace,
                    settings,
                    out var workDie)) {
                session.AlternateWorkDie = workDie;
            }

            if (session.SpawnDie == null) {
                TryFindNaturalSpawnTarget(snapshot, registry, out var spawnDie);
                session.SpawnDie = spawnDie;
            }
        }

        public static AiFloorRecoveryPhase ResolveInitialPhase(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot) {
            if (session?.AlternateWorkDie != null) {
                return AiFloorRecoveryPhase.ReachAlternateWorkDie;
            }

            if (session?.SpawnDie != null) {
                return IsReadyToMountNaturalSpawn(session, snapshot)
                    ? AiFloorRecoveryPhase.MountNaturalSpawn
                    : AiFloorRecoveryPhase.ApproachNaturalSpawn;
            }

            return AiFloorRecoveryPhase.WaitForNaturalSpawn;
        }

        public static void AdvancePhase(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (session == null) {
                return;
            }

            EnsureSessionTargets(session, snapshot, registry, settings);

            if (session.Phase == AiFloorRecoveryPhase.ApproachNaturalSpawn
                && IsReadyToMountNaturalSpawn(session, snapshot)) {
                session.Phase = AiFloorRecoveryPhase.MountNaturalSpawn;
            }

            if (session.Phase == AiFloorRecoveryPhase.WaitForNaturalSpawn
                && TryFindNaturalSpawnTarget(snapshot, registry, out var spawnDie)) {
                session.SpawnDie = spawnDie;
                session.Phase = IsReadyToMountNaturalSpawn(session, snapshot)
                    ? AiFloorRecoveryPhase.MountNaturalSpawn
                    : AiFloorRecoveryPhase.ApproachNaturalSpawn;
            }
        }

        public static bool IsReadyToMountNaturalSpawn(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot) {
            var spawnDie = session?.SpawnDie;
            if (spawnDie == null || snapshot == null || spawnDie.IsSpawning) {
                return false;
            }

            return snapshot.PlayerIsOnFloor
                && snapshot.PlayerCell == spawnDie.CurrentState.GridPos;
        }

        public static bool IsRecoveryComplete(GameStateSnapshot snapshot, AiFloorRecoverySession session) {
            if (snapshot?.StandingDice == null || session == null) {
                return false;
            }

            if (session.AlternateWorkDie != null && snapshot.StandingDice == session.AlternateWorkDie) {
                return true;
            }

            if (session.SpawnDie != null && snapshot.StandingDice == session.SpawnDie) {
                return true;
            }

            return false;
        }

        public static bool TrySelectAlternateSinkingTarget(
            GameStateSnapshot snapshot,
            int excludedTrappedFace,
            AiPlayerSettings settings,
            out DiceController workDie) {
            workDie = null;
            if (snapshot == null || settings == null) {
                return false;
            }

            var bestScore = float.MinValue;
            var found = false;

            for (var face = 2; face <= 6; face++) {
                if (excludedTrappedFace > 0 && face == excludedTrappedFace) {
                    continue;
                }

                if (!SinkingChainEvaluator.IsChainPossible(face, snapshot.PlanningDice)) {
                    continue;
                }

                var candidateGroup = SinkingChainEvaluator.GetSinkingDice(face, snapshot.PlanningDice);
                if (candidateGroup.Count == 0) {
                    continue;
                }

                if (!HasAdjacentClusterExternalDie(candidateGroup, face, snapshot.PlanningDice)) {
                    continue;
                }

                if (!ClusterSelectionEvaluator.TrySelectNearestExternalDie(
                    candidateGroup,
                    face,
                    snapshot.PlanningDice,
                    snapshot.PlayerCell,
                    settings,
                    preferChain: true,
                    out var candidateWorkDie)
                    || candidateWorkDie.Controller == null) {
                    continue;
                }

                var distance = DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, candidateWorkDie.GridPos);
                var score = candidateGroup.Count * settings.ClusterSizeWeight
                    + settings.SinkingChainBonus
                    - distance * settings.PlayerDistancePenalty;

                if (score > bestScore) {
                    bestScore = score;
                    workDie = candidateWorkDie.Controller;
                    found = true;
                }
            }

            return found;
        }

        public static bool TryFindNaturalSpawnTarget(
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            out DiceController spawnDie) {
            spawnDie = null;
            if (snapshot == null || registry == null) {
                return false;
            }

            var bestDistance = int.MaxValue;
            var found = false;

            foreach (var candidate in registry.AllDice) {
                if (!IsNaturalSpawnCandidate(snapshot, candidate)) {
                    continue;
                }

                var distance = DiceBoardAnalyzer.ManhattanDistance(
                    snapshot.PlayerCell,
                    candidate.CurrentState.GridPos);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    spawnDie = candidate;
                    found = true;
                }
            }

            return found;
        }

        static bool IsNaturalSpawnCandidate(GameStateSnapshot snapshot, DiceController candidate) {
            if (candidate == null
                || !candidate.IsSpawning
                || !candidate.AllowsUnconditionalMount
                || candidate.CurrentState.Tier != DiceStackTier.Bottom
                || candidate.IsErasing
                || candidate.IsVanishing) {
                return false;
            }

            return AiRegionFilter.IsInPlayerRegion(
                snapshot.VersusLayout,
                snapshot.PlayerSlot,
                candidate.CurrentState.GridPos);
        }

        public static bool HasAdjacentClusterExternalDie(
            IReadOnlyList<DiceSnapshot> clusterGroup,
            int clusterFace,
            IReadOnlyList<DiceSnapshot> allDice) {
            if (clusterGroup == null || clusterGroup.Count == 0 || allDice == null) {
                return false;
            }

            var clusterControllers = new HashSet<DiceController>();
            for (var i = 0; i < clusterGroup.Count; i++) {
                if (clusterGroup[i].Controller != null) {
                    clusterControllers.Add(clusterGroup[i].Controller);
                }
            }

            for (var i = 0; i < clusterGroup.Count; i++) {
                var clusterMember = clusterGroup[i];
                foreach (var adjacentCell in DiceBoardAnalyzer.GetAdjacentCells(clusterMember.GridPos)) {
                    if (TryFindMovableExternalDieAt(
                        adjacentCell,
                        clusterFace,
                        clusterControllers,
                        allDice,
                        out _)) {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool TryFindMovableExternalDieAt(
            Vector2Int cell,
            int clusterFace,
            HashSet<DiceController> clusterControllers,
            IReadOnlyList<DiceSnapshot> allDice,
            out DiceSnapshot found) {
            found = default;

            for (var i = 0; i < allDice.Count; i++) {
                var dieSnapshot = allDice[i];
                if (dieSnapshot.Controller == null || dieSnapshot.GridPos != cell) {
                    continue;
                }

                if (dieSnapshot.TopFace == clusterFace
                    || clusterControllers.Contains(dieSnapshot.Controller)) {
                    continue;
                }

                if (!DiceBoardAnalyzer.IsMovable(dieSnapshot)) {
                    continue;
                }

                found = dieSnapshot;
                return true;
            }

            return false;
        }
    }
}
