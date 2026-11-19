using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct MultiDieRollStep
    {
        public DiceController Die { get; }
        public Direction Direction { get; }

        public MultiDieRollStep(DiceController die, Direction direction) {
            Die = die;
            Direction = direction;
        }
    }

    /// <summary>
    /// Multi-die join plan: each step rolls exactly one participant one cardinal direction.
    /// <see cref="Checkpoints"/>[k] is the participant layout after k steps (k=0 is start).
    /// </summary>
    public readonly struct MultiDieRollPlan
    {
        public IReadOnlyList<DiceController> Participants { get; }
        public IReadOnlyList<MultiDieRollStep> Steps { get; }
        public IReadOnlyList<DiceState[]> Checkpoints { get; }
        public int WorkDieIndex { get; }

        public MultiDieRollPlan(
            IReadOnlyList<DiceController> participants,
            IReadOnlyList<MultiDieRollStep> steps,
            IReadOnlyList<DiceState[]> checkpoints,
            int workDieIndex) {
            Participants = participants;
            Steps = steps;
            Checkpoints = checkpoints;
            WorkDieIndex = workDieIndex;
        }

        public static MultiDieRollPlan Empty(DiceController workDie, DiceState startState) {
            return new MultiDieRollPlan(
                new[] { workDie },
                new List<MultiDieRollStep>(),
                new[] { new[] { startState } },
                workDieIndex: 0);
        }

        public DiceController WorkDie =>
            Participants != null && WorkDieIndex >= 0 && WorkDieIndex < Participants.Count
                ? Participants[WorkDieIndex]
                : null;

        public IReadOnlyList<DiceState> StartStates =>
            Checkpoints != null && Checkpoints.Count > 0 ? Checkpoints[0] : null;
    }

    public static class MultiDieRollPlanUtil
    {
        public static bool IsPlanStillValid(MultiDieRollPlan plan, int stepIndex) {
            if (plan.Participants == null
                || plan.Checkpoints == null
                || plan.Steps == null
                || stepIndex < 0
                || stepIndex > plan.Steps.Count
                || stepIndex >= plan.Checkpoints.Count) {
                return false;
            }

            return MatchesLive(plan.Participants, plan.Checkpoints[stepIndex]);
        }

        public static bool TryAdvanceCompletedSteps(MultiDieRollPlan plan, ref int stepIndex) {
            if (plan.Steps == null || plan.Checkpoints == null) {
                return false;
            }

            var advanced = false;
            while (stepIndex < plan.Steps.Count
                && stepIndex + 1 < plan.Checkpoints.Count
                && MatchesLive(plan.Participants, plan.Checkpoints[stepIndex + 1])) {
                stepIndex++;
                advanced = true;
            }

            return advanced;
        }

        public static bool TryResolveWorkDieEndState(MultiDieRollPlan plan, out DiceState endState) {
            endState = default;
            if (plan.Checkpoints == null
                || plan.Checkpoints.Count == 0
                || plan.WorkDieIndex < 0) {
                return false;
            }

            var end = plan.Checkpoints[plan.Checkpoints.Count - 1];
            if (end == null || plan.WorkDieIndex >= end.Length) {
                return false;
            }

            endState = end[plan.WorkDieIndex];
            return true;
        }

        public static bool MatchesLive(
            IReadOnlyList<DiceController> participants,
            IReadOnlyList<DiceState> expected) {
            if (participants == null || expected == null || participants.Count != expected.Count) {
                return false;
            }

            for (var i = 0; i < participants.Count; i++) {
                var die = participants[i];
                if (die == null) {
                    return false;
                }

                var live = die.CurrentState;
                var want = expected[i];
                if (live.GridPos != want.GridPos
                    || live.Tier != want.Tier
                    || live.Orientation.Top != want.Orientation.Top
                    || live.Orientation.North != want.Orientation.North
                    || live.Orientation.East != want.Orientation.East) {
                    return false;
                }
            }

            return true;
        }
    }
}
