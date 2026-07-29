using UnityEngine;

namespace DiceGame.Config
{
    public struct JumboDiceSettingsData
    {
        public bool Enabled;
        public int SequenceStartFace;
        public int SequenceEndFace;
        public int MaxPerBoard;

        public static JumboDiceSettingsData FromTemplate(JumboDiceSettings template) {
            if (template == null) {
                return Default();
            }

            return new JumboDiceSettingsData {
                Enabled = template.Enabled,
                SequenceStartFace = template.SequenceStartFace,
                SequenceEndFace = template.SequenceEndFace,
                MaxPerBoard = template.MaxPerBoard
            };
        }

        public static JumboDiceSettingsData Default() {
            return new JumboDiceSettingsData {
                Enabled = true,
                SequenceStartFace = 2,
                SequenceEndFace = 6,
                MaxPerBoard = 2
            };
        }

        public JumboDiceSettings ToRuntimeAsset() {
            return JumboDiceSettings.CreateRuntime(this);
        }

        public bool TryValidate(out string errorMessage) {
            var runtime = ToRuntimeAsset();
            var ok = runtime.TryValidate(out errorMessage);
            if (Application.isPlaying) {
                Object.Destroy(runtime);
            } else {
                Object.DestroyImmediate(runtime);
            }

            return ok;
        }
    }
}
