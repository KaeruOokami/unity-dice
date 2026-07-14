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
            AdvancePhase(session, snapshot, registry, settings);
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

        public static void InvalidateFailedTargets(AiFloorRecoverySession session, GameStateSnapshot snapshot) {
            if (session == null || snapshot == null) {
                return;
            }

            if (session.SpawnDie != null
                && snapshot.StandingDice != session.SpawnDie
                && !IsNaturalSpawnCandidate(snapshot, session.SpawnDie)) {
                AiDebugLogClearSpawn(session);
                session.SpawnDie = null;
            }

            if (session.AlternateWorkDie != null
                && snapshot.StandingDice != session.AlternateWorkDie
                && !IsLockedAlternateStillValid(
                    session.AlternateWorkDie,
                    snapshot,
                    session.SourceTrappedFace ?? 0)) {
                AiDebugLogClearAlternate(session);
                session.AlternateWorkDie = null;
            }
        }

        public static AiFloorRecoveryPhase ResolvePhase(
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

            InvalidateFailedTargets(session, snapshot);
            EnsureSessionTargets(session, snapshot, registry, settings);
            session.Phase = ResolvePhase(session, snapshot);
        }

        public static bool IsReadyToMountNaturalSpawn(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot) {
            var spawnDie = session?.SpawnDie;
            if (spawnDie == null || snapshot == null || !IsNaturalSpawnCandidate(snapshot, spawnDie)) {
                return false;
            }

            return snapshot.PlayerIsOnFloor
                && snapshot.StandingDice == null
                && snapshot.PlayerCell == spawnDie.CurrentState.GridPos;
        }

        public static bool IsReadyToMountAlternate(
            AiFloorRecoverySession session,
            GameStateSnapshot snapshot) {
            var workDie = session?.AlternateWorkDie;
            if (workDie == null || snapshot == null) {
                return false;
            }

            return snapshot.PlayerIsOnFloor
                && snapshot.StandingDice == null
                && snapshot.PlayerCell == workDie.CurrentState.GridPos;
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

        public static bool IsNaturalSpawnCandidate(GameStateSnapshot snapshot, DiceController candidate) {
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

        public static bool IsLockedAlternateStillValid(
            DiceController workDie,
            GameStateSnapshot snapshot,
            int excludedTrappedFace) {
            if (workDie == null
                || snapshot == null
                || workDie.IsErasing
                || workDie.IsVanishing) {
                return false;
            }

            for (var face = 2; face <= 6; face++) {
                if (excludedTrappedFace > 0 && face == excludedTrappedFace) {
                    continue;
                }

                if (!SinkingChainEvaluator.IsChainPossible(face, snapshot.PlanningDice)) {
                    continue;
                }

                var clusterGroup = SinkingChainEvaluator.GetSinkingDice(face, snapshot.PlanningDice);
                if (clusterGroup.Count == 0) {
                    continue;
                }

                if (IsExternalAdjacentToSinkingCluster(
                    workDie,
                    clusterGroup,
                    face,
                    snapshot.PlanningDice)) {
                    return true;
                }
            }

            return false;
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

        static bool IsExternalAdjacentToSinkingCluster(
            DiceController workDie,
            IReadOnlyList<DiceSnapshot> clusterGroup,
            int clusterFace,
            IReadOnlyList<DiceSnapshot> allDice) {
            if (workDie == null || clusterGroup == null || clusterGroup.Count == 0) {
                return false;
            }

            var clusterControllers = new HashSet<DiceController>();
            for (var i = 0; i < clusterGroup.Count; i++) {
                if (clusterGroup[i].Controller != null) {
                    clusterControllers.Add(clusterGroup[i].Controller);
                }
            }

            if (clusterControllers.Contains(workDie)) {
                return false;
            }

            var workCell = workDie.CurrentState.GridPos;
            for (var i = 0; i < clusterGroup.Count; i++) {
                foreach (var adjacentCell in DiceBoardAnalyzer.GetAdjacentCells(clusterGroup[i].GridPos)) {
                    if (adjacentCell != workCell) {
                        continue;
                    }

                    if (TryFindMovableExternalDieAt(
                        adjacentCell,
                        clusterFace,
                        clusterControllers,
                        allDice,
                        out var found)
                        && found.Controller == workDie) {
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

        static void AiDebugLogClearSpawn(AiFloorRecoverySession session) {
            Application.AiDebugLog.Log(
                $"FloorRecoveryCancel spawn={(session.SpawnDie != null ? session.SpawnDie.name : "none")} reason=became-normal");
        }

        static void AiDebugLogClearAlternate(AiFloorRecoverySession session) {
            Application.AiDebugLog.Log(
                $"FloorRecoveryCancel alternate={(session.AlternateWorkDie != null ? session.AlternateWorkDie.name : "none")} reason=cluster-no-longer-valid");
        }
    }
}
