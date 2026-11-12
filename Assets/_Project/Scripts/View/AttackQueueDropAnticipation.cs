using DiceGame.Config;
using UnityEngine;

namespace DiceGame.View
{
    /// <summary>
    /// Sharp triangular scale pulses on queue-home icons just before the head volley drops.
    /// Progress is derived from remaining queue delay so rebuilds stay in sync with spawn timing.
    /// </summary>
    public static class AttackQueueDropAnticipation
    {
        public static float EvaluateScale(float remainingDelaySeconds, AttackQueueSettings settings) {
            if (settings == null) {
                return 1f;
            }

            var duration = settings.DropAnticipationDurationSeconds;
            if (duration <= 0f || remainingDelaySeconds > duration) {
                return 1f;
            }

            var elapsed = Mathf.Clamp(duration - Mathf.Max(0f, remainingDelaySeconds), 0f, duration);
            if (elapsed >= duration) {
                return 1f;
            }

            var peakDuration = settings.DropAnticipationPeakDurationSeconds;
            var gap = settings.DropAnticipationGapSeconds;
            var peakCount = settings.DropAnticipationPeakCount;
            var cycleWithGap = peakDuration + gap;
            var leadingCycles = peakCount - 1;

            float localInPeak;
            if (elapsed < leadingCycles * cycleWithGap) {
                var inCycle = elapsed - Mathf.Floor(elapsed / cycleWithGap) * cycleWithGap;
                if (inCycle >= peakDuration) {
                    return 1f;
                }

                localInPeak = inCycle / peakDuration;
            } else {
                var inLast = elapsed - leadingCycles * cycleWithGap;
                if (inLast >= peakDuration) {
                    return 1f;
                }

                localInPeak = inLast / peakDuration;
            }

            // Sharp triangle: 0→0.5 rises to peak, 0.5→1 falls to base.
            var triangle = localInPeak < 0.5f
                ? localInPeak * 2f
                : 2f - localInPeak * 2f;
            return Mathf.Lerp(1f, settings.DropAnticipationPeakScale, triangle);
        }
    }
}
