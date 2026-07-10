using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Versus.Core
{
    public static class AttackVolumeResolver
    {
        public static int ResolveDiceCount(PlayerAttackSettings settings, float power) {
            if (settings == null || power <= 0f) {
                return 0;
            }

            var count = Mathf.RoundToInt(power * settings.MaxSendDiceCount);
            return Mathf.Clamp(count, 1, settings.MaxSendDiceCount);
        }
    }
}
