using DiceGame.Gameplay;
using DiceGame.Placement;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Converts a failed roll-join into Lift → Place for the same work die.
    /// Call only after join destinations are exhausted — never while Join is still in progress.
    /// Successful Join completion reselects the next roll-join or Lift-Join via MatchGoalSelector.
    /// </summary>
    public static class MatchGoalLiftPreference
    {
        public static bool TryConvertToLiftJoin(
            MatchGoal goal,
            GameStateSnapshot snapshot,
            DiceRegistry registry) {
            if (goal == null || snapshot == null || registry == null) {
                return false;
            }

            if (snapshot.PlayerIsCarrying || goal.IsImmediateMatch || goal.IsOnLiftJoinPath()) {
                return false;
            }

            if (goal.Face < 2 || goal.ParticipantTarget == null || goal.ClusterDice == null || goal.ClusterDice.Count == 0) {
                return false;
            }

            var workDie = new DiceSnapshot(goal.ParticipantTarget);
            if (workDie.TopFace != goal.Face) {
                return false;
            }

            if (!TryPlanLiftJoin(goal, snapshot, registry, workDie, out var plan)) {
                return false;
            }

            return goal.TryConvertIncompleteToLiftJoin(plan);
        }

        static bool TryPlanLiftJoin(
            MatchGoal goal,
            GameStateSnapshot snapshot,
            DiceRegistry registry,
            DiceSnapshot workDie,
            out LiftJoinPlan plan) {
            var joinSubGoal = goal.FindIncompleteSubGoalOfKind(AiSubGoalKind.JoinCluster);
            if (joinSubGoal != null
                && LiftJoinPlanner.TryPlanToJoinCell(
                    snapshot,
                    registry,
                    workDie,
                    joinSubGoal.TargetCell,
                    joinSubGoal.TargetTier,
                    out plan)) {
                return true;
            }

            if (SinkingChainEvaluator.IsChainPossible(goal.Face, snapshot.PlanningDice)
                && LiftJoinPlanner.TryPlanForChain(
                    snapshot,
                    registry,
                    goal.Face,
                    workDie,
                    out plan)) {
                return true;
            }

            return LiftJoinPlanner.TryPlanForCluster(
                snapshot,
                registry,
                goal.ClusterDice,
                workDie,
                goal.Face,
                snapshot.PlanningDice,
                out plan);
        }
    }
}
