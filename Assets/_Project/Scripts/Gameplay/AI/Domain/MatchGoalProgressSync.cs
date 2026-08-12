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
                        if (subGoal.TargetDie != null
                            && WorkDieSlidePlanner.IsJoinComplete(
                                subGoal.TargetDie.CurrentState,
                                subGoal.TargetCell,
                                subGoal.TargetTier,
                                subGoal.TargetFace)) {
                            subGoal.MarkComplete();
                        }
                        break;
                    case AiSubGoalKind.PlaceCarriedDie:
                        // "Not carrying" alone is ambiguous: before Lift and after Place look the same.
                        // Place is done only after any prior LiftDie is complete and we no longer carry.
                        if (!snapshot.PlayerIsCarrying && !HasIncompleteLiftBefore(goal, i)) {
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

        /// <summary>
        /// True when an earlier LiftDie in the goal is still incomplete (lift not done yet).
        /// Place-only goals have no prior Lift and return false.
        /// </summary>
        static bool HasIncompleteLiftBefore(MatchGoal goal, int placeIndex) {
            for (var i = 0; i < placeIndex; i++) {
                var prior = goal.SubGoals[i];
                if (prior.Kind == AiSubGoalKind.LiftDie && !prior.IsComplete) {
                    return true;
                }
            }

            return false;
        }
    }
}
