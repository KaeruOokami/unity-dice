using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "JumboDiceSettings", menuName = "Dice/Jumbo Dice Settings")]
    public sealed class JumboDiceSettings : ScriptableObject
    {
        [SerializeField] bool enabled = true;
        [Min(2)]
        [SerializeField] int sequenceStartFace = 2;
        [Min(2)]
        [SerializeField] int sequenceEndFace = 6;

        public bool Enabled => enabled;
        public int SequenceStartFace => sequenceStartFace;
        public int SequenceEndFace => sequenceEndFace;

        void OnValidate() {
            sequenceStartFace = Mathf.Clamp(sequenceStartFace, 2, 6);
            sequenceEndFace = Mathf.Clamp(sequenceEndFace, sequenceStartFace, 6);
        }
    }
}
