using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct NaturalSendKindLimit
    {
        [SerializeField] DiceKind kind;
        [Min(0)]
        [SerializeField] int maxCountPerVolley;
        [Min(0f)]
        [SerializeField] float selectionWeight;

        public NaturalSendKindLimit(DiceKind diceKind, int maxCount, float weight) {
            kind = diceKind;
            maxCountPerVolley = maxCount;
            selectionWeight = weight;
        }

        public DiceKind Kind => kind;
        public int MaxCountPerVolley => Mathf.Max(0, maxCountPerVolley);
        public float SelectionWeight => Mathf.Max(0f, selectionWeight);

        public bool IsEligible() {
            return MaxCountPerVolley > 0 && SelectionWeight > 0f;
        }
    }

    [CreateAssetMenu(fileName = "PlayerNaturalSendSettings", menuName = "Dice/Player Natural Send Settings")]
    public sealed class PlayerNaturalSendSettings : ScriptableObject
    {
        [SerializeField] bool enabled = true;
        [Min(1)]
        [SerializeField] int diceCountPerVolley = 1;
        [SerializeField] NaturalSendKindLimit[] sendableKinds = {
            new(DiceKind.Normal, 1, 1f)
        };

        public bool Enabled => enabled;
        public int DiceCountPerVolley => Mathf.Max(1, diceCountPerVolley);
        public NaturalSendKindLimit[] SendableKinds => sendableKinds ?? Array.Empty<NaturalSendKindLimit>();

        public bool TryValidate(out string errorMessage) {
            if (!enabled) {
                errorMessage = null;
                return true;
            }

            if (sendableKinds == null || sendableKinds.Length == 0) {
                errorMessage = "PlayerNaturalSendSettings: At least one sendable dice kind is required when enabled.";
                return false;
            }

            var hasEligible = false;
            for (var i = 0; i < sendableKinds.Length; i++) {
                if (sendableKinds[i].IsEligible()) {
                    hasEligible = true;
                    break;
                }
            }

            if (!hasEligible) {
                errorMessage = "PlayerNaturalSendSettings: No eligible sendable dice kind configured.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
