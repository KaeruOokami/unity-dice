using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;

namespace DiceGame.Versus.Core
{
    public static class NaturalSendKindBreakdown
    {
        struct KindSlot
        {
            public DiceKind Kind;
            public int Remaining;
            public float Weight;
        }

        public static bool TryBuild(
            PlayerNaturalSendSettings settings,
            int totalCount,
            System.Random random,
            out List<(DiceKind kind, int count)> breakdown) {
            breakdown = new List<(DiceKind, int)>();
            if (settings == null || !settings.Enabled || totalCount <= 0 || random == null) {
                return false;
            }

            var limits = settings.SendableKinds;
            if (limits.Length == 0) {
                return false;
            }

            var slots = new List<KindSlot>();
            var capacity = 0;
            for (var i = 0; i < limits.Length; i++) {
                var limit = limits[i];
                if (!limit.IsEligible()) {
                    continue;
                }

                slots.Add(new KindSlot {
                    Kind = limit.Kind,
                    Remaining = limit.MaxCountPerVolley,
                    Weight = limit.SelectionWeight
                });
                capacity += limit.MaxCountPerVolley;
            }

            if (slots.Count == 0) {
                return false;
            }

            if (capacity < totalCount) {
                UnityEngine.Debug.LogError(
                    $"NaturalSendKindBreakdown: sendable capacity ({capacity}) is less than requested count ({totalCount}).");
                totalCount = capacity;
            }

            var assigned = new Dictionary<DiceKind, int>();
            for (var i = 0; i < totalCount; i++) {
                if (!TryPickWeightedKind(slots, random, out var pickedIndex)) {
                    break;
                }

                var slot = slots[pickedIndex];
                slot.Remaining -= 1;
                slots[pickedIndex] = slot;

                assigned.TryGetValue(slot.Kind, out var current);
                assigned[slot.Kind] = current + 1;
            }

            foreach (var pair in assigned) {
                breakdown.Add((pair.Key, pair.Value));
            }

            return breakdown.Count > 0;
        }

        static bool TryPickWeightedKind(
            IReadOnlyList<KindSlot> slots,
            System.Random random,
            out int pickedIndex) {
            pickedIndex = -1;
            var totalWeight = 0f;

            for (var i = 0; i < slots.Count; i++) {
                if (slots[i].Remaining <= 0 || slots[i].Weight <= 0f) {
                    continue;
                }

                totalWeight += slots[i].Weight;
            }

            if (totalWeight <= 0f) {
                return false;
            }

            var roll = (float)(random.NextDouble() * totalWeight);
            var cumulative = 0f;

            for (var i = 0; i < slots.Count; i++) {
                if (slots[i].Remaining <= 0 || slots[i].Weight <= 0f) {
                    continue;
                }

                cumulative += slots[i].Weight;
                if (roll < cumulative) {
                    pickedIndex = i;
                    return true;
                }
            }

            for (var i = slots.Count - 1; i >= 0; i--) {
                if (slots[i].Remaining > 0 && slots[i].Weight > 0f) {
                    pickedIndex = i;
                    return true;
                }
            }

            return false;
        }
    }
}
