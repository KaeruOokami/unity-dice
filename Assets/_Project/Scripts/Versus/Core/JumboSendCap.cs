using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Versus.Core
{
    public static class JumboSendCap
    {
        public static int CapKindRemaining(DiceKind kind, int configuredMax, int jumboSendableRemaining) {
            var remaining = Mathf.Max(0, configuredMax);
            if (kind != DiceKind.Jumbo) {
                return remaining;
            }

            return Mathf.Min(remaining, Mathf.Max(0, jumboSendableRemaining));
        }
    }
}
