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
        public const float DefaultGravity = 25f;

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

        public static IEnumerator AnimateVerticalDropCoroutine(
            VerticalMotionState state,
            float gravity,
            float groundWorldY,
            Func<float> getHorizontalX,
            Func<float> getHorizontalZ,
            Action<float, float, float> setWorldPosition,
            Action onGrounded = null) {
            while (!state.IsGrounded) {
                state = Step(state, gravity, Time.deltaTime);
                setWorldPosition(getHorizontalX(), groundWorldY + state.Offset, getHorizontalZ());
                yield return null;
            }

            setWorldPosition(getHorizontalX(), groundWorldY, getHorizontalZ());
            onGrounded?.Invoke();
        }
    }
}
