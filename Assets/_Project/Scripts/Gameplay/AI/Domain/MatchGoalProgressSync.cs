using DiceGame.Gameplay;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class MatchGoalProgressSync
    {
        public static void Sync(MatchGoal goal, GameStateSnapshot snapshot) {
            if (goal == null || snapshot == null || goal.SubGoals == null) {
                return;
            }

            for (var i = 0; i < goal.SubGoals.Count; i++) {
                var subGoal = goal.SubGoals[i];
                if (subGoal.IsComplete) {
                    continue;
                }

                switch (subGoal.Kind) {
                    case AiSubGoalKind.ReachParticipant:
                    case AiSubGoalKind.ReachWorkDie:
                        if (subGoal.TargetDie != null && snapshot.StandingDice == subGoal.TargetDie) {
                            subGoal.MarkComplete();
                        }
                        break;
                    case AiSubGoalKind.OrientDie:
                        if (subGoal.TargetDie != null
                            && subGoal.TargetDie.CurrentState.Orientation.Top == subGoal.TargetFace) {
                            subGoal.MarkComplete();
                        }
                        break;
                    case AiSubGoalKind.JoinCluster:
                        if (subGoal.TargetDie != null) {
                            var state = subGoal.TargetDie.CurrentState;
                            if (state.GridPos == subGoal.TargetCell
                                && state.Orientation.Top == subGoal.TargetFace) {
                                subGoal.MarkComplete();
                            }
                        }
                        break;
                    case AiSubGoalKind.PlaceCarriedDie:
                        if (!snapshot.PlayerIsCarrying) {
                            subGoal.MarkComplete();
                        }
                        break;
                    case AiSubGoalKind.LiftDie:
                        if (snapshot.PlayerIsCarrying) {
                            subGoal.MarkComplete();
                        }
                        break;
                }
            }
        }
    }
}
