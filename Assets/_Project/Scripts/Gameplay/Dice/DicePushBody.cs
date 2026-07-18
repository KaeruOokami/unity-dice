using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public class DicePushBody : MonoBehaviour
    {
        [SerializeField] DiceController dice;
        BoxCollider boxCollider;

        public DiceController Dice => dice;
        public Collider Collider => boxCollider;

        void Awake() {
            boxCollider = GetComponent<BoxCollider>();
            if (dice == null) {
                dice = GetComponentInParent<DiceController>();
            }
        }

        public void Configure(Board board) {
            if (board == null) {
                return;
            }

            if (boxCollider == null) {
                boxCollider = gameObject.AddComponent<BoxCollider>();
            }

            boxCollider.isTrigger = true;
            var size = board.CellSize;
            var footprint = dice != null && dice.Capabilities.HasExpandedFootprint
                ? JumboFootprint.Size
                : 1;
            boxCollider.size = new Vector3(size * footprint, size * footprint, size * footprint);
            boxCollider.center = Vector3.zero;
        }

        public void SetCollisionEnabled(bool enabled) {
            if (boxCollider != null) {
                boxCollider.enabled = enabled;
            }
        }
    }
}
