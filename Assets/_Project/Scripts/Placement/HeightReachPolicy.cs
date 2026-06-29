using UnityEngine;

namespace DiceGame.Placement
{
    public static class HeightReachPolicy
    {
        public static bool CanStepBetween(float fromSurfaceY, float toSurfaceY, float maxStepHeight) {
            return Mathf.Abs(fromSurfaceY - toSurfaceY) <= maxStepHeight;
        }
    }
}
