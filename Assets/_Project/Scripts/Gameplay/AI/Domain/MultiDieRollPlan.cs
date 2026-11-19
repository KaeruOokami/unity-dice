using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public enum MultiDieJoinStepKind
    {
        Roll,
        Lift,
        Place
    }

    public readonly struct MultiDieJoinStep
    {
        public MultiDieJoinStepKind Kind { get; }
        public DiceController Die { get; }
        public Direction Direction { get; }
        public Vector2Int PlaceCell { get; }
        public DiceStackTier PlaceTier { get; }

        public static MultiDieJoinStep Roll(DiceController die, Direction direction) {
            return new MultiDieJoinStep(MultiDieJoinStepKind.Roll, die, direction, default, default);
        }

        public static MultiDieJoinStep Lift(DiceController die) {
            return new MultiDieJoinStep(MultiDieJoinStepKind.Lift, die, default, default, default);
        }

        public static MultiDieJoinStep Place(
            DiceController die,
            Vector2Int placeCell,
            DiceStackTier placeTier) {
            return new MultiDieJoinStep(MultiDieJoinStepKind.Place, die, default, placeCell, placeTier);
        }

        MultiDieJoinStep(
            MultiDieJoinStepKind kind,
            DiceController die,
            Direction direction,
            Vector2Int placeCell,
            DiceStackTier placeTier) {
            Kind = kind;
            Die = die;
            Direction = direction;
            PlaceCell = placeCell;
            PlaceTier = placeTier;
        }
    }

    /// <summary>
    /// Multi-die join plan: Roll / Lift / Place steps with layout checkpoints.
    /// <see cref="Checkpoints"/>[k] and <see cref="CarriedCheckpoints"/>[k] are after k steps.
    /// </summary>
    public readonly struct MultiDieRollPlan
    {
        public IReadOnlyList<DiceController> Participants { get; }
        public IReadOnlyList<MultiDieJoinStep> Steps { get; }
        public IReadOnlyList<DiceState[]> Checkpoints { get; }
        public IReadOnlyList<int> CarriedCheckpoints { get; }
        public int WorkDieIndex { get; }

        public MultiDieRollPlan(
            IReadOnlyList<DiceController> participants,
            IReadOnlyList<MultiDieJoinStep> steps,
            IReadOnlyList<DiceState[]> checkpoints,
            IReadOnlyList<int> carriedCheckpoints,
            int workDieIndex) {
            Participants = participants;
            Steps = steps;
            Checkpoints = checkpoints;
            CarriedCheckpoints = carriedCheckpoints;
            WorkDieIndex = workDieIndex;
        }

        public static MultiDieRollPlan Empty(DiceController workDie, DiceState startState) {
            return new MultiDieRollPlan(
                new[] { workDie },
                new List<MultiDieJoinStep>(),
                new[] { new[] { startState } },
                new[] { -1 },
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
                || plan.CarriedCheckpoints == null
                || plan.Steps == null
                || stepIndex < 0
                || stepIndex > plan.Steps.Count
                || stepIndex >= plan.Checkpoints.Count
                || stepIndex >= plan.CarriedCheckpoints.Count) {
                return false;
            }

            return MatchesLive(
                plan.Participants,
                plan.Checkpoints[stepIndex],
                plan.CarriedCheckpoints[stepIndex]);
        }

        public static bool TryAdvanceCompletedSteps(MultiDieRollPlan plan, ref int stepIndex) {
            if (plan.Steps == null || plan.Checkpoints == null || plan.CarriedCheckpoints == null) {
                return false;
            }

            var advanced = false;
            while (stepIndex < plan.Steps.Count
                && stepIndex + 1 < plan.Checkpoints.Count
                && stepIndex + 1 < plan.CarriedCheckpoints.Count
                && MatchesLive(
                    plan.Participants,
                    plan.Checkpoints[stepIndex + 1],
                    plan.CarriedCheckpoints[stepIndex + 1])) {
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
            return MatchesLive(participants, expected, carriedIndex: -1);
        }

        public static bool MatchesLive(
            IReadOnlyList<DiceController> participants,
            IReadOnlyList<DiceState> expected,
            int carriedIndex) {
            if (participants == null || expected == null || participants.Count != expected.Count) {
                return false;
            }

            for (var i = 0; i < participants.Count; i++) {
                var die = participants[i];
                if (die == null) {
                    return false;
                }

                if (i == carriedIndex) {
                    if (!die.IsCarried) {
                        return false;
                    }

                    continue;
                }

                if (die.IsCarried) {
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
