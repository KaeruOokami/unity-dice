using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct SendableKindLimitData
    {
        public DiceKind Kind;
        public int MaxCountPerVolley;
        public float MinimumPower;
        public float SelectionWeight;
    }

    [Serializable]
    public struct FaceAttackSendProfileData
    {
        public int[] TriggerFaces;
        public SendableKindLimitData[] SendableKinds;
    }

    public struct PlayerAttackSettingsData
    {
        public FaceAttackSendProfileData[] FaceSendProfiles;
        public float AttackMultiplier;
        public float FaceGain;
        public float ChainGain;
        public float SizeGain;
        public float SnatchMultiplier;
        public float Face2Weight;
        public float Face3Weight;
        public float Face4Weight;
        public float Face5Weight;
        public float Face6Weight;
        public float QueueToBoardDelay;

        public static PlayerAttackSettingsData FromTemplate(PlayerAttackSettings template) {
            if (template == null) {
                return Default();
            }

            var sourceProfiles = template.FaceSendProfiles;
            var profiles = new FaceAttackSendProfileData[sourceProfiles.Length];
            for (var i = 0; i < sourceProfiles.Length; i++) {
                var source = sourceProfiles[i];
                var faceSpan = source.TriggerFaces;
                var faces = new int[faceSpan.Length];
                for (var f = 0; f < faceSpan.Length; f++) {
                    faces[f] = faceSpan[f];
                }
                var kinds = source.SendableKinds;
                var kindData = new SendableKindLimitData[kinds.Length];
                for (var j = 0; j < kinds.Length; j++) {
                    kindData[j] = new SendableKindLimitData {
                        Kind = kinds[j].Kind,
                        MaxCountPerVolley = kinds[j].MaxCountPerVolley,
                        MinimumPower = kinds[j].MinimumPower,
                        SelectionWeight = kinds[j].SelectionWeight
                    };
                }

                profiles[i] = new FaceAttackSendProfileData {
                    TriggerFaces = faces,
                    SendableKinds = kindData
                };
            }

            return new PlayerAttackSettingsData {
                FaceSendProfiles = profiles,
                AttackMultiplier = template.AttackMultiplier,
                FaceGain = template.FaceGain,
                ChainGain = template.ChainGain,
                SizeGain = template.SizeGain,
                SnatchMultiplier = template.SnatchMultiplier,
                Face2Weight = template.GetFaceWeight(2),
                Face3Weight = template.GetFaceWeight(3),
                Face4Weight = template.GetFaceWeight(4),
                Face5Weight = template.GetFaceWeight(5),
                Face6Weight = template.GetFaceWeight(6),
                QueueToBoardDelay = template.QueueToBoardDelay
            };
        }

        public static PlayerAttackSettingsData Default() {
            return new PlayerAttackSettingsData {
                FaceSendProfiles = new[] {
                    new FaceAttackSendProfileData {
                        TriggerFaces = new[] { 1, 2, 3, 4, 5, 6 },
                        SendableKinds = new[] {
                            new SendableKindLimitData {
                                Kind = DiceKind.Normal,
                                MaxCountPerVolley = 3,
                                MinimumPower = 0f,
                                SelectionWeight = 1f
                            },
                            new SendableKindLimitData {
                                Kind = DiceKind.Wood,
                                MaxCountPerVolley = 2,
                                MinimumPower = 0f,
                                SelectionWeight = 1f
                            }
                        }
                    }
                },
                AttackMultiplier = 0.1f,
                FaceGain = 0.4f,
                ChainGain = 0.1f,
                SizeGain = 0.3f,
                SnatchMultiplier = 1.5f,
                Face2Weight = 1f,
                Face3Weight = 1f,
                Face4Weight = 1f,
                Face5Weight = 1f,
                Face6Weight = 1f,
                QueueToBoardDelay = 1.5f
            };
        }

        public PlayerAttackSettings ToRuntimeAsset() {
            return PlayerAttackSettings.CreateRuntime(this);
        }

        public bool TryValidate(out string errorMessage) {
            var runtime = ToRuntimeAsset();
            var ok = runtime.TryValidate(out errorMessage);
            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(runtime);
            } else {
                UnityEngine.Object.DestroyImmediate(runtime);
            }

            return ok;
        }
    }
}
