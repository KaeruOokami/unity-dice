using System;
using System.Collections;
using UnityEngine;

namespace DiceGame.Core
{
    public struct VerticalMotionState
    {
        public float Offset;
        public float VelocityY;
        public bool IsGrounded;
    }

    public static class GravityMotion
    {
        public const float DefaultGravity = 55f;

        public static float ComputeLaunchVelocity(float height, float gravity) {
            return Mathf.Sqrt(Mathf.Max(0f, 2f * gravity * height));
        }

        public static VerticalMotionState CreateLaunch(float height, float gravity) {
            return new VerticalMotionState {
                Offset = 0f,
                VelocityY = ComputeLaunchVelocity(height, gravity),
                IsGrounded = height <= 0f
            };
        }

        public static VerticalMotionState CreateDrop(float startOffset, float initialVelocityY = 0f) {
            var offset = Mathf.Max(0f, startOffset);
            return new VerticalMotionState {
                Offset = offset,
                VelocityY = initialVelocityY,
                IsGrounded = offset <= 0f
            };
        }

        public static VerticalMotionState Step(VerticalMotionState state, float gravity, float deltaTime) {
            if (state.IsGrounded || deltaTime <= 0f) {
                return state;
            }

            state.VelocityY -= gravity * deltaTime;
            state.Offset += state.VelocityY * deltaTime;

            if (state.Offset <= 0f) {
                state.Offset = 0f;
                state.VelocityY = 0f;
                state.IsGrounded = true;
            }

            return state;
        }

        /// <summary>
        /// Normalized jump timeline: 0 = launch, 0.5 = apex, 1 = landing.
        /// </summary>
        public static float ComputeFullJumpTimeline(
            VerticalMotionState motion,
            float launchVelocityY,
            float jumpHeight) {
            if (launchVelocityY > 0.001f && motion.VelocityY > 0f) {
                return 0.5f * (1f - motion.VelocityY / launchVelocityY);
            }

            var safeHeight = Mathf.Max(jumpHeight, 0.001f);
            return 0.5f + 0.5f * (1f - motion.Offset / safeHeight);
        }

        public static float ComputeRollArcProgress(float jumpTimeline, float jumpTimelineAtRollStart) {
            var remaining = 1f - jumpTimelineAtRollStart;
            if (remaining <= 0.001f) {
                return jumpTimeline >= 1f - 0.001f ? 1f : 0f;
            }

            return Mathf.Clamp01((jumpTimeline - jumpTimelineAtRollStart) / remaining);
        }

        /// <summary>
        /// Analytic free-fall time for <see cref="AnimateVerticalDropCoroutine"/>.
        /// The stepped integration always reaches the ground at or slightly before this,
        /// so a logical timer built on it never completes ahead of the visual.
        /// </summary>
        public static float ComputeDropDuration(float height, float gravity) {
            if (height <= 0f || gravity <= 0f) {
                return 0f;
            }

            return Mathf.Sqrt(2f * height / gravity);
        }

        /// <summary>
        /// Remaining airtime until <see cref="VerticalMotionState.Offset"/> reaches 0 under constant gravity.
        /// Solves ½gt² − vt − h = 0 for the positive root. Matches jump / fall visuals driven by
        /// <see cref="Step"/> (which may land one step earlier).
        /// </summary>
        public static float ComputeRemainingAirtime(VerticalMotionState motion, float gravity) {
            if (motion.IsGrounded || gravity <= 0f) {
                return 0f;
            }

            var height = Mathf.Max(0f, motion.Offset);
            var velocityY = motion.VelocityY;
            var discriminant = velocityY * velocityY + 2f * gravity * height;
            if (discriminant <= 0f) {
                return 0f;
            }

            return (velocityY + Mathf.Sqrt(discriminant)) / gravity;
        }

        public static IEnumerator AnimateVerticalDropCoroutine(
            VerticalMotionState state,
            float gravity,
            float groundWorldY,
            Func<float> getHorizontalX,
            Func<float> getHorizontalZ,
            Action<float, float, float> setWorldPosition,
            Action onGrounded = null) {
            while (!state.IsGrounded) {
                state = Step(state, gravity, GameplaySimClock.DeltaTime);
                setWorldPosition(getHorizontalX(), groundWorldY + state.Offset, getHorizontalZ());
                yield return null;
            }

            setWorldPosition(getHorizontalX(), groundWorldY, getHorizontalZ());
            onGrounded?.Invoke();
        }

        /// <summary>
        /// Spawn appearance only. Do not use for gameplay falls.
        /// </summary>
        public static VerticalMotionState StepSpawnBounce(
            VerticalMotionState state,
            float gravity,
            float deltaTime,
            float restitution,
            int maxBounceCount,
            float minBounceVelocity,
            ref int bounceCount) {
            if (state.IsGrounded || deltaTime <= 0f) {
                return state;
            }

            state.VelocityY -= gravity * deltaTime;
            state.Offset += state.VelocityY * deltaTime;

            if (state.Offset <= 0f) {
                if (-state.VelocityY > minBounceVelocity && bounceCount < maxBounceCount) {
                    state.VelocityY = -state.VelocityY * restitution;
                    state.Offset = 0f;
                    bounceCount++;
                } else {
                    state.Offset = 0f;
                    state.VelocityY = 0f;
                    state.IsGrounded = true;
                }
            }

            return state;
        }

        /// <summary>
        /// Analytic duration of <see cref="AnimateSpawnBounceDropCoroutine"/>: the initial drop
        /// plus every bounce arc the stepped integration will take.
        /// </summary>
        public static float ComputeSpawnBounceDropDuration(
            float height,
            float gravity,
            float restitution,
            int maxBounceCount,
            float minBounceVelocity) {
            var total = ComputeDropDuration(height, gravity);
            if (total <= 0f) {
                return 0f;
            }

            var impactVelocity = gravity * total;
            for (var bounce = 0; bounce < maxBounceCount; bounce++) {
                if (impactVelocity <= minBounceVelocity) {
                    break;
                }

                impactVelocity *= restitution;
                total += 2f * impactVelocity / gravity;
            }

            return total;
        }

        /// <summary>
        /// Mutable spawn-fall target so landing height can change mid-animation
        /// (e.g. pending Bottom↔Top retarget when support occupancy changes).
        /// </summary>
        public sealed class SpawnFallSession
        {
            public VerticalMotionState Motion;
            public float GroundWorldY;
        }

        /// <summary>
        /// Spawn appearance only. Do not use for gameplay falls.
        /// Reads <see cref="SpawnFallSession.GroundWorldY"/> each frame.
        /// </summary>
        public static IEnumerator AnimateSpawnBounceDropCoroutine(
            SpawnFallSession session,
            float gravity,
            float restitution,
            int maxBounceCount,
            float minBounceVelocity,
            Func<float> getHorizontalX,
            Func<float> getHorizontalZ,
            Action<float, float, float> setWorldPosition) {
            if (session == null) {
                yield break;
            }

            var bounceCount = 0;
            while (!session.Motion.IsGrounded) {
                session.Motion = StepSpawnBounce(
                    session.Motion,
                    gravity,
                    GameplaySimClock.DeltaTime,
                    restitution,
                    maxBounceCount,
                    minBounceVelocity,
                    ref bounceCount);
                setWorldPosition(
                    getHorizontalX(),
                    session.GroundWorldY + session.Motion.Offset,
                    getHorizontalZ());
                yield return null;
            }

            setWorldPosition(getHorizontalX(), session.GroundWorldY, getHorizontalZ());
        }
    }
}
