using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceSfxSettings", menuName = "Dice/Dice/Sfx Settings")]
    public class DiceSfxSettings : ScriptableObject
    {
        [Header("Impact")]
        [SerializeField] AudioClip groundContact;

        [Header("Erase")]
        [SerializeField] AudioClip erased;

        [Header("Spawn")]
        [SerializeField] AudioClip emergeFromGround;
        [SerializeField] AudioClip lightningAppear;

        [Header("Actions")]
        [SerializeField] AudioClip lift;
        [SerializeField] AudioClip throwDice;
        [SerializeField] AudioClip jump;

        /// <summary>
        /// Shared clip for aerial landings and ground-roll face settles.
        /// </summary>
        public AudioClip GroundContact => groundContact;

        public AudioClip Erased => erased;
        public AudioClip EmergeFromGround => emergeFromGround;
        public AudioClip LightningAppear => lightningAppear;
        public AudioClip Lift => lift;
        public AudioClip ThrowDice => throwDice;
        public AudioClip Jump => jump;
    }
}
