using System;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct NaturalSendKindLimitData
    {
        public DiceKind Kind;
        public int MaxCountPerVolley;
        public float SelectionWeight;
    }

    public struct PlayerNaturalSendSettingsData
    {
        public bool Enabled;
        public int DiceCountPerVolley;
        public NaturalSendKindLimitData[] SendableKinds;

        public static PlayerNaturalSendSettingsData FromTemplate(PlayerNaturalSendSettings template) {
            if (template == null) {
                return Default();
            }

            var source = template.SendableKinds;
            var kinds = new NaturalSendKindLimitData[source.Length];
            for (var i = 0; i < source.Length; i++) {
                kinds[i] = new NaturalSendKindLimitData {
                    Kind = source[i].Kind,
                    MaxCountPerVolley = source[i].MaxCountPerVolley,
                    SelectionWeight = source[i].SelectionWeight
                };
            }

            return new PlayerNaturalSendSettingsData {
                Enabled = template.Enabled,
                DiceCountPerVolley = template.DiceCountPerVolley,
                SendableKinds = kinds
            };
        }

        public static PlayerNaturalSendSettingsData Default() {
            return new PlayerNaturalSendSettingsData {
                Enabled = true,
                DiceCountPerVolley = 1,
                SendableKinds = new[] {
                    new NaturalSendKindLimitData {
                        Kind = DiceKind.Normal,
                        MaxCountPerVolley = 1,
                        SelectionWeight = 1f
                    }
                }
            };
        }

        public static PlayerNaturalSendSettingsData Empty() {
            return new PlayerNaturalSendSettingsData {
                Enabled = false,
                DiceCountPerVolley = 1,
                SendableKinds = Array.Empty<NaturalSendKindLimitData>()
            };
        }

        public PlayerNaturalSendSettings ToRuntimeAsset() {
            return PlayerNaturalSendSettings.CreateRuntime(this);
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
