using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;

namespace DiceGame.Versus.Core
{
    public static class AttackKindBreakdown
    {
        public static bool TryBuild(
            PlayerAttackSettings settings,
            int totalCount,
            System.Random random,
            out List<(DiceKind kind, int count)> breakdown) {
            breakdown = new List<(DiceKind, int)>();
            if (settings == null || totalCount <= 0 || random == null) {
                return false;
            }

            var limits = settings.SendableKinds;
            if (limits.Length == 0) {
                return false;
            }

            var remaining = new Dictionary<DiceKind, int>();
            var capacity = 0;
            for (var i = 0; i < limits.Length; i++) {
                var max = limits[i].MaxCountPerVolley;
                if (max <= 0) {
                    continue;
                }

                remaining[limits[i].Kind] = max;
                capacity += max;
            }

            if (remaining.Count == 0) {
                return false;
            }

            if (capacity < totalCount) {
                UnityEngine.Debug.LogError(
                    $"AttackKindBreakdown: total sendable capacity ({capacity}) is less than requested count ({totalCount}).");
                totalCount = capacity;
            }

            var assigned = new Dictionary<DiceKind, int>();
            for (var i = 0; i < totalCount; i++) {
                var candidates = new List<DiceKind>();
                foreach (var pair in remaining) {
                    if (pair.Value > 0) {
                        candidates.Add(pair.Key);
                    }
                }

                if (candidates.Count == 0) {
                    break;
                }

                var pick = candidates[random.Next(candidates.Count)];
                remaining[pick] -= 1;
                assigned.TryGetValue(pick, out var current);
                assigned[pick] = current + 1;
            }

            foreach (var pair in assigned) {
                breakdown.Add((pair.Key, pair.Value));
            }

            return breakdown.Count > 0;
        }
    }
}
