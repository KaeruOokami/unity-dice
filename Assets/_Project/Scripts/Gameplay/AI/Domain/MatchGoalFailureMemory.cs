using System.Collections.Generic;
using DiceGame.Gameplay;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Short-lived exclusion of goals that recently failed execution, so SelectBest can pick another hand.
    /// </summary>
    public sealed class MatchGoalFailureMemory
    {
        readonly Dictionary<int, float> excludedUntil = new Dictionary<int, float>();

        public void RememberFailure(MatchGoal goal, float durationSeconds, float nowSeconds) {
            if (goal == null || durationSeconds <= 0f) {
                return;
            }

            excludedUntil[ComputeKey(goal)] = nowSeconds + durationSeconds;
        }

        public bool IsExcluded(MatchGoal goal, float nowSeconds) {
            if (goal == null) {
                return false;
            }

            PruneExpired(nowSeconds);
            return excludedUntil.ContainsKey(ComputeKey(goal));
        }

        public void Clear() {
            excludedUntil.Clear();
        }

        public static int ComputeKey(MatchGoal goal) {
            if (goal == null) {
                return 0;
            }

            unchecked {
                var hash = goal.Face * 397;
                hash = (hash * 397) ^ ComputeControllerId(goal.ParticipantTarget);

                if (goal.ClusterDice != null) {
                    for (var i = 0; i < goal.ClusterDice.Count; i++) {
                        hash = (hash * 397) ^ ComputeControllerId(goal.ClusterDice[i].Controller);
                        hash = (hash * 397) ^ goal.ClusterDice[i].GridPos.GetHashCode();
                        hash = (hash * 397) ^ (int)goal.ClusterDice[i].Tier;
                    }
                }

                return hash;
            }
        }

        static int ComputeControllerId(DiceController controller) {
            return controller != null ? controller.GetInstanceID() : 0;
        }

        void PruneExpired(float nowSeconds) {
            if (excludedUntil.Count == 0) {
                return;
            }

            List<int> expired = null;
            foreach (var pair in excludedUntil) {
                if (pair.Value <= nowSeconds) {
                    expired ??= new List<int>();
                    expired.Add(pair.Key);
                }
            }

            if (expired == null) {
                return;
            }

            for (var i = 0; i < expired.Count; i++) {
                excludedUntil.Remove(expired[i]);
            }
        }
    }
}
