using System.Collections;
using UnityEngine;

namespace DiceGame.Core
{
    /// <summary>
    /// Shared simulation delta for lockstep dual-sim.
    /// When inactive, consumers use <see cref="Time.deltaTime"/> (offline / local play).
    /// When active, time advances only for lockstep ticks (stalls freeze dice visuals and timers).
    /// </summary>
    public static class GameplaySimClock
    {
        static bool active;
        static float framePresentationDelta;
        static float currentStepDelta;

        public static bool IsActive => active;

        /// <summary>
        /// Delta for gameplay/visual consumers. Prefer this over <see cref="Time.deltaTime"/>.
        /// </summary>
        public static float DeltaTime {
            get {
                if (!active) {
                    return Time.deltaTime;
                }

                if (currentStepDelta > 0f) {
                    return currentStepDelta;
                }

                return framePresentationDelta;
            }
        }

        public static void SetActive(bool enabled) {
            active = enabled;
            if (!enabled) {
                framePresentationDelta = 0f;
                currentStepDelta = 0f;
            }
        }

        /// <summary>
        /// Call once at the start of each Unity frame before lockstep steps.
        /// </summary>
        public static void BeginUnityFrame() {
            if (!active) {
                return;
            }

            framePresentationDelta = 0f;
            currentStepDelta = 0f;
        }

        /// <summary>
        /// Call around one lockstep simulation step so in-step consumers see fixed dt.
        /// </summary>
        public static void BeginStep(float deltaTime) {
            if (!active) {
                return;
            }

            currentStepDelta = deltaTime > 0f ? deltaTime : 0f;
        }

        public static void EndStep() {
            if (!active) {
                return;
            }

            framePresentationDelta += currentStepDelta;
            currentStepDelta = 0f;
        }

        /// <summary>
        /// Wait that advances with <see cref="DeltaTime"/> (pauses during lockstep stalls).
        /// </summary>
        public static IEnumerator WaitForSeconds(float seconds) {
            var elapsed = 0f;
            var target = Mathf.Max(0f, seconds);
            while (elapsed < target) {
                elapsed += DeltaTime;
                yield return null;
            }
        }
    }
}
