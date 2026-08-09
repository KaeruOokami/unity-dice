using System;

namespace DiceGame.Core
{
    /// <summary>
    /// Copied from production <c>Assets/_Project/Scripts/Core/Motion/GravityMotion.cs</c>
    /// (pure math only; Unity coroutines live in <c>GravityMotionPlayback</c>).
    /// Mathf → Math for noEngine.
    /// </summary>
    public struct VerticalMotionState
    {
        public float Offset;
        public float VelocityY;
        public bool IsGrounded;
    }

    public static class GravityMotion
    {
        public const float DefaultGravity = 55f;

        public static float ComputeLaunchVelocity(float height, float gravity)
        {
            return (float)Math.Sqrt(Math.Max(0f, 2f * gravity * height));
        }

        public static VerticalMotionState CreateLaunch(float height, float gravity)
        {
            return new VerticalMotionState
            {
                Offset = 0f,
                VelocityY = ComputeLaunchVelocity(height, gravity),
                IsGrounded = height <= 0f
            };
        }

        public static VerticalMotionState CreateDrop(float startOffset, float initialVelocityY = 0f)
        {
            var offset = Math.Max(0f, startOffset);
            return new VerticalMotionState
            {
                Offset = offset,
                VelocityY = initialVelocityY,
                IsGrounded = offset <= 0f
            };
        }

        public static VerticalMotionState Step(VerticalMotionState state, float gravity, float deltaTime)
        {
            if (state.IsGrounded || deltaTime <= 0f)
            {
                return state;
            }

            state.VelocityY -= gravity * deltaTime;
            state.Offset += state.VelocityY * deltaTime;

            if (state.Offset <= 0f)
            {
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
            float jumpHeight)
        {
            if (launchVelocityY > 0.001f && motion.VelocityY > 0f)
            {
                return 0.5f * (1f - motion.VelocityY / launchVelocityY);
            }

            var safeHeight = Math.Max(jumpHeight, 0.001f);
            var t = 0.5f + 0.5f * (1f - motion.Offset / safeHeight);
            if (t < 0f)
            {
                return 0f;
            }

            if (t > 1f)
            {
                return 1f;
            }

            return t;
        }

        public static float ComputeRollArcProgress(float jumpTimeline, float jumpTimelineAtRollStart)
        {
            var remaining = 1f - jumpTimelineAtRollStart;
            if (remaining <= 0.001f)
            {
                return jumpTimeline >= 1f - 0.001f ? 1f : 0f;
            }

            var p = (jumpTimeline - jumpTimelineAtRollStart) / remaining;
            if (p < 0f)
            {
                return 0f;
            }

            return p > 1f ? 1f : p;
        }

        public static float ComputeDropDuration(float height, float gravity)
        {
            if (height <= 0f || gravity <= 0f)
            {
                return 0f;
            }

            return (float)Math.Sqrt(2f * height / gravity);
        }

        public static float ComputeRemainingAirtime(VerticalMotionState motion, float gravity)
        {
            if (motion.IsGrounded || gravity <= 0f)
            {
                return 0f;
            }

            var height = Math.Max(0f, motion.Offset);
            var velocityY = motion.VelocityY;
            var discriminant = velocityY * velocityY + 2f * gravity * height;
            if (discriminant <= 0f)
            {
                return 0f;
            }

            return (velocityY + (float)Math.Sqrt(discriminant)) / gravity;
        }

        public static VerticalMotionState StepSpawnBounce(
            VerticalMotionState state,
            float gravity,
            float deltaTime,
            float restitution,
            int maxBounceCount,
            float minBounceVelocity,
            ref int bounceCount)
        {
            if (state.IsGrounded || deltaTime <= 0f)
            {
                return state;
            }

            state.VelocityY -= gravity * deltaTime;
            state.Offset += state.VelocityY * deltaTime;

            if (state.Offset <= 0f)
            {
                if (-state.VelocityY > minBounceVelocity && bounceCount < maxBounceCount)
                {
                    state.VelocityY = -state.VelocityY * restitution;
                    state.Offset = 0f;
                    bounceCount++;
                }
                else
                {
                    state.Offset = 0f;
                    state.VelocityY = 0f;
                    state.IsGrounded = true;
                }
            }

            return state;
        }

        public static float ComputeSpawnBounceDropDuration(
            float height,
            float gravity,
            float restitution,
            int maxBounceCount,
            float minBounceVelocity)
        {
            var total = ComputeDropDuration(height, gravity);
            if (total <= 0f)
            {
                return 0f;
            }

            var impactVelocity = gravity * total;
            for (var bounce = 0; bounce < maxBounceCount; bounce++)
            {
                if (impactVelocity <= minBounceVelocity)
                {
                    break;
                }

                impactVelocity *= restitution;
                total += 2f * impactVelocity / gravity;
            }

            return total;
        }

        public sealed class SpawnFallSession
        {
            public VerticalMotionState Motion;
            public float GroundWorldY;
        }
    }
}
