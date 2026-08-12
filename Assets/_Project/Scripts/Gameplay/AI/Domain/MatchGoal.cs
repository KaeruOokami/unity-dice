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
        readonly HashSet<long> failedJoinSlotKeys = new HashSet<long>();

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

        public ISet<long> FailedJoinSlotKeys => failedJoinSlotKeys;

        public void RememberFailedJoinTarget(Vector2Int targetCell, DiceStackTier targetTier) {
            failedJoinSlotKeys.Add(WorkDieSlidePlanner.JoinSlotKey(targetCell, targetTier));
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

        public void ClearFailedJoinTargets() {
            failedJoinSlotKeys.Clear();
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
        readonly List<AiSubGoal> subGoals;

        public int Face { get; }
        public IReadOnlyList<DiceSnapshot> ClusterDice { get; }
        public DiceController ParticipantTarget { get; }
        public IReadOnlyList<AiSubGoal> SubGoals => subGoals;
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
            this.subGoals = subGoals != null
                ? new List<AiSubGoal>(subGoals)
                : new List<AiSubGoal>();
            PriorityScore = priorityScore;
            IsImmediateMatch = isImmediateMatch;
        }

        public void MarkUnplannable() {
            IsMarkedUnplannable = true;
        }

        /// <summary>
        /// Drop incomplete Reach/Orient/Join and finish via Lift → Place (same work die).
        /// Used only after roll-join destinations are exhausted, not mid-Join.
        /// </summary>
        public bool TryConvertIncompleteToLiftJoin(LiftJoinPlan plan) {
            if (plan.WorkDie == null || plan.WorkDie != ParticipantTarget) {
                return false;
            }

            if (IsOnLiftJoinPath()) {
                return false;
            }

            for (var i = subGoals.Count - 1; i >= 0; i--) {
                if (!subGoals[i].IsComplete) {
                    subGoals.RemoveAt(i);
                }
            }

            subGoals.Add(AiSubGoal.LiftDie(plan.WorkDie));
            subGoals.Add(AiSubGoal.PlaceCarriedDie(plan.PlaceCell));
            return true;
        }

        public bool IsOnLiftJoinPath() {
            return HasIncompleteSubGoalOfKind(AiSubGoalKind.LiftDie)
                || HasIncompleteSubGoalOfKind(AiSubGoalKind.PlaceCarriedDie);
        }

        public AiSubGoal GetNextIncompleteSubGoal() {
            for (var i = 0; i < subGoals.Count; i++) {
                if (!subGoals[i].IsComplete) {
                    return subGoals[i];
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

            // Keep carrying goals only while the planned place cell is orthogonally adjacent.
            if (snapshot.PlayerIsCarrying && HasIncompleteSubGoalOfKind(AiSubGoalKind.PlaceCarriedDie)) {
                var placeSubGoal = FindIncompleteSubGoalOfKind(AiSubGoalKind.PlaceCarriedDie);
                if (placeSubGoal != null
                    && DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, placeSubGoal.TargetCell) == 1) {
                    return false;
                }

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

        /// <summary>
        /// Priority/score goal switches (including Face changes) are locked while Join is still
        /// in progress, while Lift→Place join is underway, or while the work die can still
        /// roll-join the cluster. Face may switch only after cluster join is no longer possible.
        /// </summary>
        public bool AllowsPriorityGoalSwitch(GameStateSnapshot snapshot, DiceRegistry registry) {
            if (snapshot == null) {
                return false;
            }

            if (HasIncompleteSubGoalOfKind(AiSubGoalKind.JoinCluster)) {
                return false;
            }

            if (IsOnLiftJoinPath()) {
                return false;
            }

            if (Face < 2 || ParticipantTarget == null || ClusterDice == null || ClusterDice.Count == 0) {
                return true;
            }

            return !WorkDieSlidePlanner.CanWorkDieExtendCluster(
                ClusterDice,
                new DiceSnapshot(ParticipantTarget),
                snapshot.PlanningDice,
                registry,
                snapshot.VersusLayout,
                snapshot.PlayerSlot);
        }

        /// <summary>
        /// Different work-die switches are allowed only when this work die can no longer extend the cluster.
        /// Same work die (or non-join goals) may switch on score alone when <see cref="AllowsPriorityGoalSwitch"/> permits.
        /// </summary>
        public bool AllowsWorkDieSwitch(
            MatchGoal candidate,
            GameStateSnapshot snapshot,
            DiceRegistry registry) {
            if (candidate == null || snapshot == null) {
                return false;
            }

            if (ParticipantTarget == null || candidate.ParticipantTarget == null) {
                return true;
            }

            if (ParticipantTarget == candidate.ParticipantTarget) {
                return true;
            }

            if (Face < 2 || ClusterDice == null || ClusterDice.Count == 0) {
                return true;
            }

            return !WorkDieSlidePlanner.CanWorkDieExtendCluster(
                ClusterDice,
                new DiceSnapshot(ParticipantTarget),
                snapshot.PlanningDice,
                registry,
                snapshot.VersusLayout,
                snapshot.PlayerSlot);
        }

        public AiSubGoal FindIncompleteSubGoalOfKind(AiSubGoalKind kind) {
            for (var i = 0; i < subGoals.Count; i++) {
                if (subGoals[i].Kind == kind && !subGoals[i].IsComplete) {
                    return subGoals[i];
                }
            }

            return null;
        }

        bool HasIncompleteSubGoalOfKind(AiSubGoalKind kind) {
            return FindIncompleteSubGoalOfKind(kind) != null;
        }
    }
}
