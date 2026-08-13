using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Cluster quality for ML shaping: growth is credited on increase only,
    /// hold is the current quality each decision so brief spikes still keep the
    /// growth reward while maintained clusters keep paying.
    /// </summary>
    public static class MlProgressRewardEvaluator
    {
        public static float ComputeProgressScore(GameStateSnapshot snapshot, MlAgentSettings settings) {
            if (snapshot == null || settings == null || !settings.ProgressShapingEnabled) {
                return 0f;
            }

            var planningDice = snapshot.PlanningDice;
            if (planningDice == null || planningDice.Count == 0) {
                return 0f;
            }

            var best = 0f;
            for (var face = 2; face <= 6; face++) {
                var clusters = DiceBoardAnalyzer.FindFaceClusters(planningDice, face);
                for (var i = 0; i < clusters.Count; i++) {
                    var cluster = clusters[i];
                    if (cluster == null || cluster.Count == 0) {
                        continue;
                    }

                    var progress = Mathf.Clamp01(cluster.Count / (float)face);
                    var score = progress * settings.ClusterProgressWeight
                        + cluster.Count * settings.ClusterSizeProgressWeight;

                    if (SinkingChainEvaluator.IsChainPossible(face, planningDice)) {
                        score += settings.ChainPotentialBonus;
                    }

                    if (score > best) {
                        best = score;
                    }
                }
            }

            return best;
        }

        public static float ComputeHoldReward(float quality, MlAgentSettings settings) {
            if (settings == null || quality <= 0f) {
                return 0f;
            }

            return quality * settings.ClusterHoldWeight;
        }

        public static float ComputeGrowthReward(float currentQuality, float previousQuality) {
            var delta = currentQuality - previousQuality;
            return delta > 0f ? delta : 0f;
        }
    }
}
