using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "JumboDiceSettings", menuName = "Dice/Jumbo Dice Settings")]
    public sealed class JumboDiceSettings : ScriptableObject
    {
        /// <summary>
        /// Slots reserved on each player board for elimination-start jumbo sequence.
        /// </summary>
        public const int SequenceReserveCount = 1;

        [SerializeField] bool enabled = true;
        [Min(2)]
        [SerializeField] int sequenceStartFace = 2;
        [Min(2)]
        [SerializeField] int sequenceEndFace = 6;
        [Min(1)]
        [SerializeField] int maxPerBoard = 2;

        public bool Enabled => enabled;
        public int SequenceStartFace => sequenceStartFace;
        public int SequenceEndFace => sequenceEndFace;
        public int MaxPerBoard => Mathf.Max(1, maxPerBoard);
        public int MaxSendablePerBoard => Mathf.Max(0, MaxPerBoard - SequenceReserveCount);

        public int GetRemainingSendableSlots(int currentJumboCountOnBoard) {
            return Mathf.Max(0, MaxSendablePerBoard - Mathf.Max(0, currentJumboCountOnBoard));
        }

        void OnValidate() {
            sequenceStartFace = Mathf.Clamp(sequenceStartFace, 2, 6);
            sequenceEndFace = Mathf.Clamp(sequenceEndFace, sequenceStartFace, 6);
            maxPerBoard = Mathf.Max(1, maxPerBoard);
        }

        public bool TryValidate(out string errorMessage) {
            if (MaxPerBoard < SequenceReserveCount) {
                errorMessage =
                    $"JumboDiceSettings: MaxPerBoard ({MaxPerBoard}) must be >= SequenceReserveCount ({SequenceReserveCount}).";
                return false;
            }

            if (sequenceStartFace < 2 || sequenceStartFace > 6) {
                errorMessage = "JumboDiceSettings: SequenceStartFace must be in range 2-6.";
                return false;
            }

            if (sequenceEndFace < sequenceStartFace || sequenceEndFace > 6) {
                errorMessage = "JumboDiceSettings: SequenceEndFace must be in range SequenceStartFace-6.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public static JumboDiceSettings CreateRuntime(JumboDiceSettingsData data) {
            var instance = CreateInstance<JumboDiceSettings>();
            instance.Apply(data);
            return instance;
        }

        public void Apply(JumboDiceSettingsData data) {
            enabled = data.Enabled;
            sequenceStartFace = Mathf.Clamp(data.SequenceStartFace, 2, 6);
            sequenceEndFace = Mathf.Clamp(data.SequenceEndFace, sequenceStartFace, 6);
            maxPerBoard = Mathf.Max(1, data.MaxPerBoard);
        }
    }
}
