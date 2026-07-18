using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
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
        public Vector2Int TargetCell { get; private set; }
        public DiceStackTier TargetTier { get; private set; }
        public bool IsComplete { get; private set; }
        public WorkDieSlidePlan? JoinSlidePlan { get; private set; }
        public int JoinSlideStepIndex { get; private set; }
        public WorkDieSlidePlan? OrientRollPlan { get; private set; }
        public int OrientRollStepIndex { get; private set; }

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
            ClearJoinSlidePlan();
            ClearOrientRollPlan();
        }

        public bool HasJoinSlidePlan => JoinSlidePlan.HasValue;

        public bool HasOrientRollPlan => OrientRollPlan.HasValue;

        public void SetJoinSlidePlan(WorkDieSlidePlan plan) {
            JoinSlidePlan = plan;
            JoinSlideStepIndex = 0;
        }

        public void SetOrientRollPlan(WorkDieSlidePlan plan) {
            OrientRollPlan = plan;
            OrientRollStepIndex = 0;
        }

        public void ClearJoinSlidePlan() {
            JoinSlidePlan = null;
            JoinSlideStepIndex = 0;
        }

        public void ClearOrientRollPlan() {
            OrientRollPlan = null;
            OrientRollStepIndex = 0;
        }

        public void RetargetJoin(Vector2Int targetCell, DiceStackTier targetTier) {
            if (Kind != AiSubGoalKind.JoinCluster) {
                return;
            }

            TargetCell = targetCell;
            TargetTier = targetTier;
            ClearJoinSlidePlan();
        }

        public bool TryAdvanceJoinSlideStep(DiceState state) {
            if (!JoinSlidePlan.HasValue) {
                return false;
            }

            var plan = JoinSlidePlan.Value;
            var stepIndex = JoinSlideStepIndex;
            if (!WorkDieSlidePlanner.TryAdvanceCompletedSteps(plan, ref stepIndex, state)) {
                return false;
            }

            JoinSlideStepIndex = stepIndex;
            return true;
        }

        public bool TryAdvanceOrientRollStep(DiceState state) {
            if (!OrientRollPlan.HasValue) {
                return false;
            }

            var plan = OrientRollPlan.Value;
            var stepIndex = OrientRollStepIndex;
            if (!WorkDieSlidePlanner.TryAdvanceCompletedSteps(plan, ref stepIndex, state)) {
                return false;
            }

            OrientRollStepIndex = stepIndex;
            return true;
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
        public bool IsMarkedUnplannable { get; private set; }

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

        public void MarkUnplannable() {
            IsMarkedUnplannable = true;
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

        public bool IsStale(GameStateSnapshot snapshot, AiPlayerSettings settings, DiceRegistry registry = null) {
            if (snapshot == null) {
                return true;
            }

            if (IsMarkedUnplannable) {
                return true;
            }

            if (ParticipantTarget == null
                || ParticipantTarget.IsSpawning
                || ParticipantTarget.IsErasing
                || ParticipantTarget.IsSinkErasing) {
                return true;
            }

            if (snapshot.StandingDice != null && snapshot.StandingDice.IsSinkErasing) {
                return true;
            }

            if (registry != null && Face >= 2 && ClusterDice != null && ClusterDice.Count > 0) {
                if (IsImmediateMatch
                    && ClusterSelectionEvaluator.ShouldDiscardImmediateCluster(ClusterDice, registry)) {
                    return true;
                }

                if (!IsImmediateMatch
                    && ClusterSelectionEvaluator.ShouldDiscardIncompleteCluster(
                        ClusterDice,
                        Face,
                        snapshot,
                        registry)) {
                    return true;
                }
            }

            if (ParticipantTarget != null
                && !ClusterSelectionEvaluator.IsStandableWorkDie(
                    new DiceSnapshot(ParticipantTarget),
                    snapshot.PlanningDice,
                    registry)) {
                return true;
            }

            if (!IsImmediateMatch
                && settings != null
                && ClusterSelectionEvaluator.IsStrandedIsolatedNonSinkingCluster(
                    snapshot,
                    Face,
                    ClusterDice)
                && ClusterSelectionEvaluator.HasRetargetableCluster(
                    snapshot,
                    Face,
                    ClusterDice,
                    settings,
                    registry)) {
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
