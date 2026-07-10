using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct SendableKindLimit
    {
        [SerializeField] DiceKind kind;
        [Min(0)]
        [SerializeField] int maxCountPerVolley;

        public SendableKindLimit(DiceKind diceKind, int maxCount) {
            kind = diceKind;
            maxCountPerVolley = maxCount;
        }

        public DiceKind Kind => kind;
        public int MaxCountPerVolley => Mathf.Max(0, maxCountPerVolley);
    }

    [CreateAssetMenu(fileName = "PlayerAttackSettings", menuName = "Dice/Player Attack Settings")]
    public sealed class PlayerAttackSettings : ScriptableObject
    {
        [Header("Sendable Dice")]
        [SerializeField] SendableKindLimit[] sendableKinds = {
            new(DiceKind.Normal, 3),
            new(DiceKind.Wood, 2)
        };
        [Min(1)]
        [SerializeField] int maxSendDiceCount = 3;

        [Header("Power")]
        [Min(0f)]
        [SerializeField] float attackMultiplier = 1f;
        [Min(0f)]
        [SerializeField] float chainGain = 0.2f;
        [Min(0f)]
        [SerializeField] float sizeGain = 0.1f;
        [Min(0f)]
        [SerializeField] float snatchMultiplier = 1.5f;

        [Header("Queue")]
        [Min(0f)]
        [SerializeField] float queueToBoardDelay = 1.5f;

        [Header("Dissolve Visual")]
        [SerializeField] Color dissolveEmissionColor = new(0.4f, 0.8f, 1f, 1f);

        public SendableKindLimit[] SendableKinds => sendableKinds ?? Array.Empty<SendableKindLimit>();
        public int MaxSendDiceCount => Mathf.Max(1, maxSendDiceCount);
        public float AttackMultiplier => Mathf.Max(0f, attackMultiplier);
        public float ChainGain => Mathf.Max(0f, chainGain);
        public float SizeGain => Mathf.Max(0f, sizeGain);
        public float SnatchMultiplier => Mathf.Max(0f, snatchMultiplier);
        public float QueueToBoardDelay => Mathf.Max(0f, queueToBoardDelay);
        public Color DissolveEmissionColor => dissolveEmissionColor;

        public bool TryValidate(out string errorMessage) {
            if (sendableKinds == null || sendableKinds.Length == 0) {
                errorMessage = "PlayerAttackSettings: At least one sendable dice kind is required.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
