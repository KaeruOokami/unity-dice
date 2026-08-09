using System;
using System.Collections;
using UnityEngine;

namespace DiceGame.Core
{
    /// <summary>
    /// Unity playback helpers extracted from production <see cref="GravityMotion"/> coroutines.
    /// Pure step / duration APIs live in the noEngine <see cref="GravityMotion"/> library type.
    /// </summary>
    public static class GravityMotionPlayback
    {
        public static IEnumerator AnimateVerticalDropCoroutine(
            VerticalMotionState state,
            float gravity,
            float groundWorldY,
            Func<float> getHorizontalX,
            Func<float> getHorizontalZ,
            Action<float, float, float> setWorldPosition,
            Action onGrounded = null)
        {
            while (!state.IsGrounded)
            {
                state = GravityMotion.Step(state, gravity, GameplaySimClock.DeltaTime);
                setWorldPosition(getHorizontalX(), groundWorldY + state.Offset, getHorizontalZ());
                yield return null;
            }

            setWorldPosition(getHorizontalX(), groundWorldY, getHorizontalZ());
            onGrounded?.Invoke();
        }

        public static IEnumerator AnimateSpawnBounceDropCoroutine(
            GravityMotion.SpawnFallSession session,
            float gravity,
            float restitution,
            int maxBounceCount,
            float minBounceVelocity,
            Func<float> getHorizontalX,
            Func<float> getHorizontalZ,
            Action<float, float, float> setWorldPosition)
        {
            if (session == null)
            {
                yield break;
            }

            var bounceCount = 0;
            while (!session.Motion.IsGrounded)
            {
                session.Motion = GravityMotion.StepSpawnBounce(
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
