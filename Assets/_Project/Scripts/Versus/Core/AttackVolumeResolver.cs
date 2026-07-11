using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Versus.Core
{
    public static class AttackVolumeResolver
    {
        public static int ResolveMaxVolleyCount(SendableKindLimit[] sendableKinds, float power) {
            if (sendableKinds == null || sendableKinds.Length == 0 || power <= 0f) {
                return 0;
            }

            var maxVolley = 0;
            for (var i = 0; i < sendableKinds.Length; i++) {
                var limit = sendableKinds[i];
                if (!limit.IsEligibleAtPower(power)) {
                    continue;
                }

                maxVolley = Mathf.Max(maxVolley, limit.MaxCountPerVolley);
            }

            return maxVolley;
        }

        public static int ResolveDiceCount(SendableKindLimit[] sendableKinds, float power) {
            var maxVolley = ResolveMaxVolleyCount(sendableKinds, power);
            if (maxVolley <= 0) {
                return 0;
            }

            var count = Mathf.RoundToInt(power * maxVolley);
            return Mathf.Clamp(count, 0, maxVolley);
        }
    }
}
