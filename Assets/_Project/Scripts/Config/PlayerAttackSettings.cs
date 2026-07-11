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
        [Range(0f, 1f)]
        [SerializeField] float minimumPower;
        [Min(0f)]
        [SerializeField] float selectionWeight;

        public SendableKindLimit(DiceKind diceKind, int maxCount) {
            kind = diceKind;
            maxCountPerVolley = maxCount;
            minimumPower = 0f;
            selectionWeight = 1f;
        }

        public DiceKind Kind => kind;
        public int MaxCountPerVolley => Mathf.Max(0, maxCountPerVolley);
        public float MinimumPower => Mathf.Clamp01(minimumPower);
        public float SelectionWeight => Mathf.Max(0f, selectionWeight);

        public bool IsEligibleAtPower(float power) {
            return MaxCountPerVolley > 0
                && SelectionWeight > 0f
                && power >= MinimumPower;
        }
    }

    [Serializable]
    public struct FaceAttackSendProfile
    {
        [SerializeField] int[] triggerFaces;
        [SerializeField] SendableKindLimit[] sendableKinds;

        public FaceAttackSendProfile(int[] faces, params SendableKindLimit[] kinds) {
            triggerFaces = faces;
            sendableKinds = kinds;
        }

        public ReadOnlySpan<int> TriggerFaces => triggerFaces ?? Array.Empty<int>();
        public SendableKindLimit[] SendableKinds => sendableKinds ?? Array.Empty<SendableKindLimit>();
        public int TriggerFaceCount => triggerFaces?.Length ?? 0;

        public bool ContainsFace(int face) {
            if (triggerFaces == null) {
                return false;
            }

            for (var i = 0; i < triggerFaces.Length; i++) {
                if (triggerFaces[i] == face) {
                    return true;
                }
            }

            return false;
        }
    }

    [CreateAssetMenu(fileName = "PlayerAttackSettings", menuName = "Dice/Player Attack Settings")]
    public sealed class PlayerAttackSettings : ScriptableObject
    {
        const int MinTriggerFace = 1;
        const int MaxTriggerFace = 6;

        [Header("Sendable Dice")]
        [SerializeField] FaceAttackSendProfile[] faceSendProfiles = {
            new(new[] { 1, 2, 3, 4, 5, 6 }, new SendableKindLimit(DiceKind.Normal, 3), new SendableKindLimit(DiceKind.Wood, 2))
        };
        [Min(1)]
        [SerializeField] int maxSendDiceCount = 3;

        [Header("Power")]
        [Min(0f)]
        [SerializeField] float attackMultiplier = 0.1f;
        [Min(0f)]
        [SerializeField] float faceGain = 0.4f;
        [Min(0f)]
        [SerializeField] float chainGain = 0.1f;
        [Min(0f)]
        [SerializeField] float sizeGain = 0.3f;
        [Min(0f)]
        [SerializeField] float snatchMultiplier = 1.5f;

        [Header("Face Weights (multiply total power)")]
        [Min(0f)]
        [SerializeField] float face2Weight = 1f;
        [Min(0f)]
        [SerializeField] float face3Weight = 1f;
        [Min(0f)]
        [SerializeField] float face4Weight = 1f;
        [Min(0f)]
        [SerializeField] float face5Weight = 1f;
        [Min(0f)]
        [SerializeField] float face6Weight = 1f;

        [Header("Queue")]
        [Min(0f)]
        [SerializeField] float queueToBoardDelay = 1.5f;

        [Header("Dissolve Visual")]
        [SerializeField] Color dissolveEmissionColor = new(0.4f, 0.8f, 1f, 1f);

        public FaceAttackSendProfile[] FaceSendProfiles => faceSendProfiles ?? Array.Empty<FaceAttackSendProfile>();
        public int MaxSendDiceCount => Mathf.Max(1, maxSendDiceCount);
        public float AttackMultiplier => Mathf.Max(0f, attackMultiplier);
        public float FaceGain => Mathf.Max(0f, faceGain);
        public float ChainGain => Mathf.Max(0f, chainGain);
        public float SizeGain => Mathf.Max(0f, sizeGain);
        public float SnatchMultiplier => Mathf.Max(0f, snatchMultiplier);
        public float QueueToBoardDelay => Mathf.Max(0f, queueToBoardDelay);
        public Color DissolveEmissionColor => dissolveEmissionColor;

        public float GetFaceWeight(int face) {
            return face switch {
                2 => Mathf.Max(0f, face2Weight),
                3 => Mathf.Max(0f, face3Weight),
                4 => Mathf.Max(0f, face4Weight),
                5 => Mathf.Max(0f, face5Weight),
                6 => Mathf.Max(0f, face6Weight),
                _ => 1f
            };
        }

        public bool TryGetSendableKindsForFace(int face, out SendableKindLimit[] sendableKinds) {
            sendableKinds = null;
            var profiles = FaceSendProfiles;
            if (profiles.Length == 0) {
                return false;
            }

            var bestIndex = -1;
            var bestSpecificity = int.MaxValue;

            for (var i = 0; i < profiles.Length; i++) {
                var profile = profiles[i];
                if (!profile.ContainsFace(face)) {
                    continue;
                }

                var specificity = profile.TriggerFaceCount;
                if (specificity >= bestSpecificity) {
                    continue;
                }

                bestSpecificity = specificity;
                bestIndex = i;
            }

            if (bestIndex < 0) {
                return false;
            }

            sendableKinds = profiles[bestIndex].SendableKinds;
            return sendableKinds.Length > 0;
        }

        public bool TryValidate(out string errorMessage) {
            var profiles = FaceSendProfiles;
            if (profiles.Length == 0) {
                errorMessage = "PlayerAttackSettings: At least one face send profile is required.";
                return false;
            }

            var coveredFaces = new bool[MaxTriggerFace];
            for (var i = 0; i < profiles.Length; i++) {
                var profile = profiles[i];
                if (profile.TriggerFaceCount == 0) {
                    errorMessage = $"PlayerAttackSettings: faceSendProfiles[{i}] must specify at least one trigger face.";
                    return false;
                }

                var triggerFaces = profile.TriggerFaces;
                for (var j = 0; j < triggerFaces.Length; j++) {
                    var triggerFace = triggerFaces[j];
                    if (triggerFace < MinTriggerFace || triggerFace > MaxTriggerFace) {
                        errorMessage =
                            $"PlayerAttackSettings: faceSendProfiles[{i}].triggerFaces contains invalid face {triggerFace}. Valid range is {MinTriggerFace}-{MaxTriggerFace}.";
                        return false;
                    }

                    coveredFaces[triggerFace - 1] = true;
                }

                if (profile.SendableKinds.Length == 0) {
                    errorMessage = $"PlayerAttackSettings: faceSendProfiles[{i}] must specify at least one sendable dice kind.";
                    return false;
                }
            }

            for (var face = MinTriggerFace; face <= MaxTriggerFace; face++) {
                if (!coveredFaces[face - 1]) {
                    errorMessage =
                        $"PlayerAttackSettings: No face send profile covers erasure face {face}. Each face from {MinTriggerFace} to {MaxTriggerFace} must appear in at least one triggerFaces list.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
