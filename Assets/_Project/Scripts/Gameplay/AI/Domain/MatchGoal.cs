using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public enum AiSubGoalKind
    {
        ReachParticipant,
        ReachWorkDie,
        OrientDie,
        JoinCluster,
        PlaceCarriedDie,
        LiftDie
    }

    public sealed class AiSubGoal
    {
        public AiSubGoalKind Kind { get; }
        public DiceController TargetDie { get; }
        public int TargetFace { get; }
        public Vector2Int TargetCell { get; }
        public DiceStackTier TargetTier { get; }
        public bool IsComplete { get; private set; }

        AiSubGoal(
            AiSubGoalKind kind,
            DiceController targetDie,
            int targetFace,
            Vector2Int targetCell,
            DiceStackTier targetTier) {
            Kind = kind;
            TargetDie = targetDie;
            TargetFace = targetFace;
            TargetCell = targetCell;
            TargetTier = targetTier;
        }

        public static AiSubGoal ReachParticipant(DiceController die) {
            return new AiSubGoal(AiSubGoalKind.ReachParticipant, die, 0, die.CurrentState.GridPos, die.CurrentState.Tier);
        }

        public static AiSubGoal ReachWorkDie(DiceController die) {
            return new AiSubGoal(AiSubGoalKind.ReachWorkDie, die, 0, die.CurrentState.GridPos, die.CurrentState.Tier);
        }

        public static AiSubGoal OrientDie(DiceController die, int face) {
            var state = die.CurrentState;
            return new AiSubGoal(AiSubGoalKind.OrientDie, die, face, state.GridPos, state.Tier);
        }

        public static AiSubGoal JoinCluster(DiceController die, int face, Vector2Int targetCell, DiceStackTier tier) {
            return new AiSubGoal(AiSubGoalKind.JoinCluster, die, face, targetCell, tier);
        }

        public static AiSubGoal LiftDie(DiceController die) {
            var state = die.CurrentState;
            return new AiSubGoal(AiSubGoalKind.LiftDie, die, 0, state.GridPos, state.Tier);
        }

        public static AiSubGoal PlaceCarriedDie(Vector2Int targetCell) {
            return new AiSubGoal(AiSubGoalKind.PlaceCarriedDie, null, 0, targetCell, DiceStackTier.Bottom);
        }

        public void MarkComplete() {
            IsComplete = true;
        }
    }

    public sealed class MatchGoal
    {
        public int Face { get; }
        public IReadOnlyList<DiceSnapshot> ClusterDice { get; }
        public DiceController ParticipantTarget { get; }
        public IReadOnlyList<AiSubGoal> SubGoals { get; }
        public float PriorityScore { get; }
        public bool IsImmediateMatch { get; }

        public MatchGoal(
            int face,
            IReadOnlyList<DiceSnapshot> clusterDice,
            DiceController participantTarget,
            IReadOnlyList<AiSubGoal> subGoals,
            float priorityScore,
            bool isImmediateMatch) {
            Face = face;
            ClusterDice = clusterDice;
            ParticipantTarget = participantTarget;
            SubGoals = subGoals;
            PriorityScore = priorityScore;
            IsImmediateMatch = isImmediateMatch;
        }

        public AiSubGoal GetNextIncompleteSubGoal() {
            for (var i = 0; i < SubGoals.Count; i++) {
                if (!SubGoals[i].IsComplete) {
                    return SubGoals[i];
                }
            }

            return null;
        }

        public bool AreAllSubGoalsComplete() {
            return GetNextIncompleteSubGoal() == null;
        }

        public bool IsStale(GameStateSnapshot snapshot) {
            if (snapshot == null) {
                return true;
            }

            if (ParticipantTarget == null
                || ParticipantTarget.IsSpawning
                || ParticipantTarget.IsErasing) {
                return true;
            }

            if (snapshot.PlayerIsCarrying && Face == 0) {
                return false;
            }

            if (snapshot.PlayerIsCarrying && !HasIncompleteSubGoalOfKind(AiSubGoalKind.PlaceCarriedDie)) {
                return true;
            }

            return false;
        }

        public bool ShouldSwitchTo(MatchGoal candidate, float switchMargin) {
            if (candidate == null) {
                return false;
            }

            return candidate.PriorityScore > PriorityScore + switchMargin;
        }

        bool HasIncompleteSubGoalOfKind(AiSubGoalKind kind) {
            for (var i = 0; i < SubGoals.Count; i++) {
                if (SubGoals[i].Kind == kind && !SubGoals[i].IsComplete) {
                    return true;
                }
            }

            return false;
        }
    }
}
